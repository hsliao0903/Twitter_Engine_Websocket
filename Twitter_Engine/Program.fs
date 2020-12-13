open System
open System.Diagnostics
open System.Security.Cryptography
open System.Globalization
open System.Collections.Generic
open System.Text
open Akka.Actor
open Akka.FSharp
open ActorOFDB
open TwitterServerCollections
open WebSocketSharp.Server
open UpdateDBActors

(* For Json Libraries *)
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json



// (* Actor System Configuration Settings (Server) Side) *)
// let config =
//     Configuration.parse
//         @"akka {
//             log-config-on-start = off
//             log-dead-letters = off
//             log-dead-letters-during-shutdown = off
//             actor {
//                 provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""

//             }


//             remote {
//                 log-received-messages = off
//                 log-sent-messages = off
//                 helios.tcp {
//                     hostname = localhost
//                     port = 9001
//                 }
//             }
//         }"

// let system = System.create "TwitterEngine" config

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




// //let dbActorRef = spawn system "db1" queryActorNode
// let mutable debugMode = false
// let mutable showStatusMode = 0
// let globalTimer = Stopwatch()


// (* Actor Nodes *)
// let serverActorNode (serverMailbox:Actor<string>) =
//     let nodeName = serverMailbox.Self.Path.Name
    
//     // if user successfully connected (login), add the user to a set
//     let mutable msgProcessed = 0
//     let mutable onlineUserSet = Set.empty
//     let updateOnlineUserDB userID option = 
//         let isConnected = onlineUserSet.Contains(userID)
//         if option = "connect" && not isConnected then
//             if isValidUser userID then
//                 onlineUserSet <- onlineUserSet.Add(userID)
//                 0
//             else
//                 -1
//         else if option = "disconnect" && isConnected then
//             onlineUserSet <- onlineUserSet.Remove(userID)
//             0
//         else
//             0
//     (* Server status variables to count requests and tweets *)
//     let mutable totalRequests = 0
//     let mutable maxThrougput = 0
    
//     let showServerStatus _ =
//         totalRequests <- totalRequests + msgProcessed
//         maxThrougput <- Math.Max(maxThrougput, msgProcessed)
//         if showStatusMode = 0 then    
//             printfn "\n---------- Server Status ---------"
//             printfn "Total Processed Requests: %i" totalRequests
//             printfn "Request Porcessed Last Second: %i" msgProcessed
//             printfn "Max Server Throughput: %i" maxThrougput   //message processed per second
//             printfn "Total Tweets in DB: %i" (tweetMap.Keys.Count)
//             printfn "Total Registered Users: %i" (regMap.Keys.Count)
//             printfn "Online Users: %i" (onlineUserSet.Count)
//             printfn "----------------------------------\n"
//         msgProcessed <- 0

//     let timer = new Timers.Timer(1000.0)
//     timer.Elapsed.Add(showServerStatus)
//     timer.Start()


//     (* Server Actor Function *)
//     let rec loop() = actor {
//         let! (message: string) = serverMailbox.Receive()
//         let  sender = serverMailbox.Sender()
//         let  jsonMsg = JsonValue.Parse(message)
//         let  reqType = jsonMsg?ReqType.AsString()
//         let  userID = jsonMsg?UserID.AsInteger()

//         match reqType with
//             | "Register" ->
//                 (* Save the register information into data strucute *)
//                 (* Check if the userID has already registered before *)
//                 let regMsg = (Json.deserialize<RegInfo> message)
//                 if debugMode then
//                     printfn "[%s] Received Register Request from User%s" nodeName (sender.Path.Name)
                
//                 let status = updateRegDB regMsg
//                 let reply:ReplyInfo = { 
//                     ReqType = "Reply" ;
//                     Type = reqType ;
//                     Status =  status ;
//                     Desc =  Some (regMsg.UserID.ToString()) ;
//                 }
                
//                 (* Reply for the register satus *)
//                 sender <!  (Json.serialize reply)

//             | "SendTweet" ->
//                 //totalTweets <- totalTweets + 1
//                 let orgtweetInfo = (Json.deserialize<TweetInfo> message)
//                 let tweetInfo = assignTweetID orgtweetInfo
//                 if debugMode then
//                     printfn "[%s] Received a Tweet reqeust from User%s" nodeName (sender.Path.Name)
//                     printfn "%A" tweetInfo
//                 (* Store the informations for this tweet *)
//                 (* Check if the userID has already registered? if not, don't accept this Tweet *)
//                 if (isValidUser tweetInfo.UserID) then
//                     updateTweetDB tweetInfo

//                     let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = reqType ;
//                         Status =  "Success" ;
//                         Desc =  Some "Successfully send a Tweet to Server" ;
//                     }
//                     sender <! (Json.serialize reply)
//                 else
//                     let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = reqType ;
//                         Status =  "Failed" ;
//                         Desc =  Some "The user should be registered before sending a Tweet" ;
//                     }
//                     sender <! (Json.serialize reply)

//             | "Retweet" ->
//                 let retweetID = jsonMsg?RetweetID.AsString()
//                 let tUserID = jsonMsg?TargetUserID.AsInteger()
//                 let mutable isFail = false
                
//                 (* user might assign a specific retweetID or empty string *)
//                 if retweetID = "" then
//                     (* make sure the target user has at least one tweet in his history *)
//                     if (isValidUser tUserID) && historyMap.ContainsKey(tUserID) && historyMap.[tUserID].Count > 0 then
//                         (* random pick one tweet from the target user's history *)
//                         let rnd = Random()
//                         let numTweet = historyMap.[tUserID].Count
//                         let rndIdx = rnd.Next(numTweet)
//                         let targetReTweetID = historyMap.[tUserID].[rndIdx]
//                         let retweetInfo = tweetMap.[targetReTweetID]
//                         //let keyArray = Array.create (totalNum) ""
//                         //tweetMap.Keys.CopyTo(keyArray, 0)

//                         (* check if the author is the one who send retweet request *)
//                         if (retweetInfo.UserID <> userID) then
//                             updateRetweet userID retweetInfo
//                         else
//                             isFail <- true
//                     else
//                         isFail <- true
//                 else
//                     (* Check if it is a valid retweet ID in tweetDB *)
//                     if tweetMap.ContainsKey(retweetID) then
//                         (* check if the author is the one who send retweet request *)
//                         if (tweetMap.[retweetID].UserID) <> userID then
//                             updateRetweet userID (tweetMap.[retweetID])
//                         else
//                             isFail <- true
//                     else
//                         isFail <- true

//                 (* Deal with reply message *)
//                 if isFail then
//                     let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = "SendTweet" ;
//                         Status =  "Failed" ;
//                         Desc =  Some "The random choose of retweet fails (same author situation)" ;
//                     }
//                     sender <! (Json.serialize reply)
//                 else
//                     let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = "SendTweet" ;
//                         Status =  "Success" ;
//                         Desc =  Some "Successfully retweet the Tweet!" ;
//                     }
//                     sender <! (Json.serialize reply)
//                 //return! loop()
//             | "Subscribe" ->
//                 let status = updatePubSubDB (jsonMsg?PublisherID.AsInteger()) (jsonMsg?UserID.AsInteger())
//                 let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = reqType ;
//                         Status =  status ;
//                         Desc =  None ;
//                 }
//                 sender <! (Json.serialize reply)

//             | "Connect" ->
//                 let userID = jsonMsg?UserID.AsInteger()
//                 (* Only allow user to query after successfully connected (login) and registered *)
//                 let ret = (updateOnlineUserDB userID "connect")
//                 if ret < 0 then
//                     let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = reqType ;
//                         Status =  "Fail" ;
//                         Desc =  Some "Please register first" ;
//                     }
//                     sender <! (Json.serialize reply)
//                 else 
//                     let (reply:ReplyInfo) = { 
//                         ReqType = "Reply" ;
//                         Type = reqType ;
//                         Status =  "Success" ;
//                         Desc =  Some (userID.ToString()) ;
//                     }
//                     sender <! (Json.serialize reply)
                
//             | "Disconnect" ->
                
//                 (* if disconnected, user cannot query or send tweet *)
//                 (updateOnlineUserDB userID "disconnect") |> ignore
//                 let (reply:ReplyInfo) = { 
//                     ReqType = "Reply" ;
//                     Type = reqType ;
//                     Status =  "Success" ;
//                     Desc =   Some (userID.ToString()) ;
//                 }
//                 sender <! (Json.serialize reply)

           
     

                   

//             | _ ->
//                 printfn "client \"%s\" received unknown message \"%s\"" nodeName reqType
//                 Environment.Exit 1

//         msgProcessed <- msgProcessed + 1
//         return! loop()
//     }
//     loop()


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
            
            if reqType = "Connect" then
                (* Only allow user to query after successfully connected (login) and registered *)
                let ret = (updateOnlineUserDB userID "connect")
                if ret < 0 then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Fail" ;
                        Desc =  Some "Please register first" ;
                    }
                    let data = (Json.serialize reply)
                    sessionManager.SendTo(data,sid)
                else 
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Success" ;
                        Desc =  Some (userID.ToString()) ;
                    }
                    let data = (Json.serialize reply)
                    sessionManager.SendTo(data,sid)
            else
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
        printfn "[/register] sessionID:%A Data:%s" wssm.ID message.Data 
        regActorRef <! WsockToRegActor (message.Data,connectionActorRef , wssm.Sessions, wssm.ID)

type Tweet () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/tweet/send] sessionID:%A Data:%s" wssm.ID message.Data 
        tweetActorRef <! WsockToActor (message.Data, wssm.Sessions, wssm.ID)

type Retweet () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/tweet/retweet] sessionID:%A Data:%s" wssm.ID message.Data 
        retweetActorRef <! WsockToActor (message.Data, wssm.Sessions, wssm.ID)

type Subscribe () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/subscribe] sessionID:%A Data:%s" wssm.ID message.Data 
        subscriveActorRef <! WsockToActor (message.Data,wssm.Sessions,wssm.ID)

type Connection () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/connection] sessionID:%A Data:%s" (wssm.ID) (message.Data)
        connectionActorRef <! WsockToConActor (message.Data,wssm.Sessions,wssm.ID)

type QueryHis () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/tweet/query] sessionID:%A Data:%s" wssm.ID message.Data
        queryHisActorRef <! WsockToQActor (message.Data, getRandomWorker(), wssm.Sessions, wssm.ID)

type QueryMen () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/mention/query] sessionID:%A Data:%s" wssm.ID message.Data
        queryMenActorRef <! WsockToQActor (message.Data, getRandomWorker(), wssm.Sessions, wssm.ID)

type QueryTag () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/tag/query] sessionID:%A Data:%s" wssm.ID message.Data
        queryTagActorRef <! WsockToQActor (message.Data, getRandomWorker(), wssm.Sessions, wssm.ID)
type QuerySub () =
    inherit WebSocketBehavior()
    override wssm.OnMessage message = 
        printfn "[/subscribe/query] sessionID:%A Data:%s" wssm.ID message.Data
        querySubActorRef <! WsockToQActor (message.Data, getRandomWorker() , wssm.Sessions, wssm.ID)




[<EntryPoint>]
let main argv =
    try
        // (* Check if the parameter is "debug" or not, if yes, set the Server to debug mode, to get more ouptputs of requests *)
        // if argv.Length <> 0 then
        //     debugMode <- 
        //         match (argv.[0]) with
        //         | "debug" -> true
        //         | _ -> false
       
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
        printfn "Server start...."
        Console.ReadLine() |> ignore
        wss.Stop()
        
        // globalTimer.Start()
        
 

    with | :? IndexOutOfRangeException ->
            printfn "\n[Main] Incorrect Inputs or IndexOutOfRangeException!\n"

         | :?  FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 // return an integer exit code
