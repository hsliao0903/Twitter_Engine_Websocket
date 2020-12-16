open System
open System.Diagnostics
open System.Security.Cryptography
open System.Globalization
open System.Collections.Generic
open System.Text
open Akka.Actor
open Akka.FSharp

open ActorOFDB
open Authentication
open TwitterServerCollections
open WebSocketSharp.Server
open UpdateDBActors

(* For Json Libraries *)
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json

let system = ActorSystem.Create("TwitterEngine")

let numQueryWorker = 1000
let spawnQueryActors (clientNum: int) = 
    [1 .. clientNum]
    |> List.map (fun id -> (spawn system ("Q"+id.ToString()) queryActorNode))
    |> List.toArray
let myQueryWorker = spawnQueryActors numQueryWorker
let getRandomWorker () =
    let rnd = Random()
    myQueryWorker.[rnd.Next(numQueryWorker)]

//
//   Actor for Connection Request
//

let mutable onlineUserSet = Set.empty
let updateOnlineUserDB userID option = 
    let isConnected = onlineUserSet.Contains(userID)
    if option = "connect" && not isConnected then
        if isValidUser userID then
            onlineUserSet <- onlineUserSet.Add(userID)
            0
        else
            -1
    else if option = "disconnect" && isConnected then
        onlineUserSet <- onlineUserSet.Remove(userID)
        0
    else
        0

let connectionActor (serverMailbox:Actor<ConActorMsg>) =
    let nodeName = serverMailbox.Self.Path.Name
    let rec loop() = actor {

        let! (message: ConActorMsg) = serverMailbox.Receive()
        match message with
        | WsockToConActor (msg, sessionManager, sid) ->
            let connectionInfo = (Json.deserialize<ConnectInfo> msg)
            let userID = connectionInfo.UserID
            let reqType = connectionInfo.ReqType           
            
            match reqType with
            | "Connect" ->
                (* Only allow user to query after successfully connected (login) and registered *)
                if not (onlineUserSet.Contains(userID)) && isValidUser userID then
                    let ch = generateChallenge
                    challengeCaching userID ch |> Async.Start
                    if isAuthDebug then
                        printfn "Generate challenge: %A\nAnd then cache the challenge for 1 second..." ch
                        printfn "[timestamp] %A\n" (DateTime.Now)
                    let (reply:ConnectReply) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Auth" ;
                        Authentication = ch;
                        Desc =  Some (userID.ToString());
                    }
                    let data = (Json.serialize reply)
                    sessionManager.SendTo(data,sid)  
                else 
                    let (reply:ConnectReply) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Fail" ;
                        Authentication = "";
                        Desc =  Some ("Please register first") ;
                    }
                    let data = (Json.serialize reply)
                    sessionManager.SendTo(data,sid)                         
            | "Auth" ->
                let keyInfo = keyMap.[userID]
                if (challengeCache.ContainsKey userID) then
                    let answer = challengeCache.[userID]
                    let signature = connectionInfo.Signature
                    if isAuthDebug then
                        printfn "[timestamp] Server rx authentication reply from user at %A" DateTime.Now
                        printfn "Server try to verify the signed challenge sent from User%i..." userID
                        printfn "Retrive the cached challenge and then verfiy with user's public key"
                    if (verifySignature answer signature keyInfo.UserPublicKey) then
                        let (reply:ConnectReply) = { 
                            ReqType = "Reply" ;
                            Type = reqType ;
                            Status =  "Success" ;
                            Authentication = "";
                            Desc =  Some (userID.ToString());
                        }
                        let data = (Json.serialize reply)
                        sessionManager.SendTo(data,sid)  
                        // serverMailbox.Self <! AutoConnect userID
                    else
                        printfn "\n[Error] Authentication failed for User%i\n" userID
                        let (reply:ConnectReply) = { 
                            ReqType = "Reply" ;
                            Type = reqType ;
                            Status =  "Fail" ;
                            Desc =  Some "Authentication failed!" ;
                            Authentication = "";
                        }
                        let data = (Json.serialize reply)
                        sessionManager.SendTo(data,sid)
                else
                    let (reply:ConnectReply) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Fail" ;
                        Desc =  Some "Authentication failed!" ;
                        Authentication = "";
                    }
                    let data = (Json.serialize reply)
                    sessionManager.SendTo(data,sid)
            | _ ->
                (* if disconnected, user cannot query or send tweet *)
                (updateOnlineUserDB userID "disconnect") |> ignore
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = reqType ;
                    Status =  "Success" ;
                    Desc =   Some (userID.ToString()) ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)
        | AutoConnect userID ->
            // If register success, then auto connect this userID to server
            let ret = updateOnlineUserDB userID "connect"
            if ret< 0 then printfn "[userID:%i] Auto connect to server failed " userID
            else printfn "[UserID:%i] Auto connect to server succeed!" userID

        return! loop()
    }
    loop() 





//////////////////////////////////////////////
// 
// WebSocket Implementation
//  
////////////////////////////////////////////////

// websocket server
let wss = WebSocketServer("ws://localhost:9001")

// different kind of DB actors
let regActorRef = spawn system "Register-DB-Worker" registerActor
let tweetActorRef = spawn system "AddTweet-DB-Worker" tweetActor
let retweetActorRef = spawn system "ReTweet-DB-Worker" retweetActor
let subscriveActorRef = spawn system "Subscrive-DB-Worker" subscribeActor
let connectionActorRef = spawn system "Connection-DB-Worker" connectionActor
let queryHisActorRef = spawn system "QHistory-DB-Worker" queryHistoryActor
let queryMenActorRef = spawn system "QMention-DB-Worker" queryMentionActor
let queryTagActorRef = spawn system "QTag-DB-Worker" queryTagActor
let querySubActorRef = spawn system "QSub-DB-Worker" querySubActor

type Register () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/register]\nData:%s\n" message.Data 
        regActorRef <! WsockToRegActor (message.Data, connectionActorRef, wssm.Sessions, wssm.ID)

type Tweet () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/tweet/send]\nData:%s\n" message.Data 
        tweetActorRef <! WsockToActor (message.Data, wssm.Sessions, wssm.ID)

type Retweet () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/tweet/retweet]\nData:%s\n"  message.Data 
        retweetActorRef <! WsockToActor (message.Data, wssm.Sessions, wssm.ID)

type Subscribe () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/subscribe]\nData:%s\n" message.Data 
        subscriveActorRef <! WsockToActor (message.Data,wssm.Sessions,wssm.ID)

type Connection () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/connection]\nData:%s\n" (message.Data)
        connectionActorRef <! WsockToConActor (message.Data,wssm.Sessions,wssm.ID)

type QueryHis () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/tweet/query]\nData:%s\n" message.Data
        queryHisActorRef <! WsockToQActor (message.Data, getRandomWorker(), wssm.Sessions, wssm.ID)

type QueryMen () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/mention/query]\nData:%s\n" message.Data
        queryMenActorRef <! WsockToQActor (message.Data, getRandomWorker(), wssm.Sessions, wssm.ID)

type QueryTag () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/tag/query]\nData:%s\n" message.Data
        queryTagActorRef <! WsockToQActor (message.Data, getRandomWorker(), wssm.Sessions, wssm.ID)
type QuerySub () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "\n[/subscribe/query]\nData:%s\n" message.Data
        querySubActorRef <! WsockToQActor (message.Data, getRandomWorker() , wssm.Sessions, wssm.ID)




[<EntryPoint>]
let main argv =
    try
        if argv.Length <> 0 then
            isAuthDebug <- 
                match (argv.[0]) with
                | "debug" -> true
                | _ -> false

        wss.AddWebSocketService<Register> ("/register")
        wss.AddWebSocketService<Tweet> ("/tweet/send")
        wss.AddWebSocketService<Retweet> ("/tweet/retweet")
        wss.AddWebSocketService<Subscribe> ("/subscribe")
        wss.AddWebSocketService<Connection> ("/connect")
        wss.AddWebSocketService<Connection> ("/disconnect")
        wss.AddWebSocketService<QueryHis> ("/tweet/query")
        wss.AddWebSocketService<QueryMen> ("/mention/query")
        wss.AddWebSocketService<QueryTag> ("/tag/query")
        wss.AddWebSocketService<QuerySub> ("/subscribe/query")
        wss.Start ()
        printfn "\n-------------------------------------"
        if isAuthDebug then
            printfn "Twitter server start.... \n[Debug Authentication Mode On]"
        else
            printfn "Twitter server start...."
        printfn "-------------------------------------\n"
        Console.ReadLine() |> ignore
        wss.Stop()
 

    with | :? IndexOutOfRangeException ->
            printfn "\n[Main] Incorrect Inputs or IndexOutOfRangeException!\n"

         | :?  FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 // return an integer exit code
