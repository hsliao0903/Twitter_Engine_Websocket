module UserAPIs

open System
open Message
open UserInterface
open WebSocketSharp
open FSharp.Json
open System.Collections.Generic


let createWebsocketDB serverWebsockAddr =
    let wssActorDB = new Dictionary<string, WebSocket>()
    wssActorDB.Add("Register", new WebSocket(serverWebsockAddr      +   "/register"))
    wssActorDB.Add("SendTweet", new WebSocket(serverWebsockAddr     +   "/tweet/send"))
    wssActorDB.Add("Retweet", new WebSocket(serverWebsockAddr       +   "/tweet/retweet"))
    wssActorDB.Add("Subscribe", new WebSocket(serverWebsockAddr     +   "/subscribe"))
    wssActorDB.Add("Connect", new WebSocket(serverWebsockAddr       +   "/connect"))
    wssActorDB.Add("Disconnect", new WebSocket(serverWebsockAddr    +   "/disconnect"))
    wssActorDB.Add("QueryHistory", new WebSocket(serverWebsockAddr  +   "/tweet/query"))
    wssActorDB.Add("QueryMention", new WebSocket(serverWebsockAddr  +   "/mention/query"))
    wssActorDB.Add("QueryTag", new WebSocket(serverWebsockAddr      +   "/tag/query"))
    wssActorDB.Add("QuerySubscribe", new WebSocket(serverWebsockAddr +  "/subscribe/query"))
    wssActorDB
    
let enableWss (wssDB:Dictionary<string, WebSocket>) =
    wssDB.["SendTweet"].Connect()
    wssDB.["Retweet"].Connect()
    wssDB.["Subscribe"].Connect()
    wssDB.["Disconnect"].Connect()
    wssDB.["QueryHistory"].Connect()
    wssDB.["QueryMention"].Connect()
    wssDB.["QueryTag"].Connect()
    wssDB.["QuerySubscribe"].Connect()

let disableWss (wssDB:Dictionary<string, WebSocket>) =
    wssDB.["SendTweet"].Close()
    wssDB.["Retweet"].Close()
    wssDB.["Subscribe"].Close()
    wssDB.["Disconnect"].Close()
    wssDB.["QueryHistory"].Close()
    wssDB.["QueryMention"].Close()
    wssDB.["QueryTag"].Close()
    wssDB.["QuerySubscribe"].Close()


// 
// Websocket OnMessage Callback Functions
// 

// Register reuqest callback
let regCallback (nodeName, wssDB:Dictionary<string,WebSocket>, isSimulation:bool) = fun (msg:MessageEventArgs) ->
    let replyInfo = (Json.deserialize<ReplyInfo> msg.Data)
    let isSuccess = if (replyInfo.Status = "Success") then (true) else (false)

    if isSuccess then
        enableWss (wssDB)
        if isSimulation then 
            printfn "[%s] User \"%s\" registered and auto login successfully" nodeName (replyInfo.Desc.ToString())
        else isUserModeLoginSuccess <- Success

    else
        if isSimulation then printfn "[%s] Register failed!\n" nodeName
        else isUserModeLoginSuccess <- Fail
    // Close the session for /register
    wssDB.["Register"].Close()

let error = fun (arg:ErrorEventArgs) ->
    printfn "[Error]"


// 
// Client Actor Node Helper Functinos
// 

let sendRegMsgToServer (msg:string, isSimulation, wssReg:WebSocket, nodeID) =
    wssReg.Connect()
    if isSimulation then
        let regMsg:RegJson = { 
            ReqType = "Register" ; 
            UserID = nodeID ; 
            UserName = "User"+ (nodeID.ToString()) ; 
            PublicKey = Some ("Key") ;
        }
        let data = (Json.serialize regMsg)
        wssReg.Send(data)
    else
        wssReg.Send(msg)