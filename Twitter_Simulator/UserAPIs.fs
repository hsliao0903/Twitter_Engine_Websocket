module UserAPIs

open System
open Message
open UserInterface
open WebSocketSharp
open FSharp.Json
open FSharp.Data
open FSharp.Data.JsonExtensions
open System.Collections.Generic


let createWebsocketDB serverWebsockAddr =
    let wssActorDB = new Dictionary<string, WebSocket>()
    (wssActorDB.Add("Register", new WebSocket(serverWebsockAddr      +   "/register")))
    (wssActorDB.Add("SendTweet", new WebSocket(serverWebsockAddr     +   "/tweet/send")))
    (wssActorDB.Add("Retweet", new WebSocket(serverWebsockAddr       +   "/tweet/retweet")))
    (wssActorDB.Add("Subscribe", new WebSocket(serverWebsockAddr     +   "/subscribe")))
    (wssActorDB.Add("Connect", new WebSocket(serverWebsockAddr       +   "/connect")))
    (wssActorDB.Add("Disconnect", new WebSocket(serverWebsockAddr    +   "/disconnect")))
    (wssActorDB.Add("QueryHistory", new WebSocket(serverWebsockAddr  +   "/tweet/query")))
    (wssActorDB.Add("QueryMention", new WebSocket(serverWebsockAddr  +   "/mention/query")))
    (wssActorDB.Add("QueryTag", new WebSocket(serverWebsockAddr      +   "/tag/query")))
    (wssActorDB.Add("QuerySubscribe", new WebSocket(serverWebsockAddr +  "/subscribe/query")))
    wssActorDB
    
let enableWss (wssDB:Dictionary<string, WebSocket>) =
    (wssDB.["SendTweet"].Connect())
    (wssDB.["Retweet"].Connect())
    (wssDB.["Subscribe"].Connect())
    (wssDB.["Disconnect"].Connect())
    (wssDB.["QueryHistory"].Connect())
    (wssDB.["QueryMention"].Connect())
    (wssDB.["QueryTag"].Connect())
    (wssDB.["QuerySubscribe"].Connect())

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
            printfn "[%s] User \"%s\" registered and auto login successfully" nodeName (replyInfo.Desc.Value)
        else isUserModeLoginSuccess <- Success

    else
        if isSimulation then printfn "[%s] Register failed!\n" nodeName
        else isUserModeLoginSuccess <- Fail
    // Close the session for /register
    wssDB.["Register"].Close()


let connectCallback (nodeName, wssDB:Dictionary<string,WebSocket>) = fun (msg:MessageEventArgs) ->
    let replyInfo = (Json.deserialize<ReplyInfo> msg.Data)
    let isSuccess = if (replyInfo.Status = "Success") then (true) else (false)

    if isSuccess then
        enableWss (wssDB)
        isUserModeLoginSuccess <- Success
        
        (* Automatically query the history tweets of the connected user *)
        let (queryMsg:QueryInfo) = {
            ReqType = "QueryHistory" ;
            UserID = (replyInfo.Desc.Value|> int) ;
            Tag = "" ;
        }
        wssDB.["QueryHistory"].Send(Json.serialize queryMsg)

    else
        isUserModeLoginSuccess <- Fail
    wssDB.["Connect"].Close()

let disconnectCallback (nodeName, wssDB:Dictionary<string,WebSocket>) = fun (msg:MessageEventArgs) ->
    disableWss (wssDB)
    isUserModeLoginSuccess <- Success


let replyCallback (nodeName) = fun (msg:MessageEventArgs) ->
    let replyInfo = (Json.deserialize<ReplyInfo> msg.Data)
    let isSuccess = if (replyInfo.Status = "Success") then (true) else (false)
    if isSuccess then printfn "[%s] %s" nodeName (replyInfo.Desc.Value)
    else printfn "[%s] [Error] %s" nodeName (replyInfo.Desc.Value)

let printTweet message = 
    let tweetReplyInfo = (Json.deserialize<TweetReply> message)
    let tweetInfo = tweetReplyInfo.TweetInfo
    printfn "\n------------------------------------"
    printfn "Index: %i      Time: %s" (tweetReplyInfo.Status) (tweetInfo.Time.ToString())
    printfn "Author: User%i" (tweetInfo.UserID)
    let mentionStr = if (tweetInfo.Mention < 0) then "@N/A" else ("@User"+tweetInfo.Mention.ToString())
    let tagStr = if (tweetInfo.Tag = "") then "#N/A" else (tweetInfo.Tag)
    printfn "Content: {%s}\n%s  %s  Retweet times: %i" (tweetInfo.Content) (tagStr) (mentionStr) (tweetInfo.RetweetTimes)
    printfn "TID: %s" (tweetInfo.TweetID)

let printSubscribe message nodeName =
    let subReplyInfo = (Json.deserialize<SubReply> message)
    printfn "\n------------------------------------"
    printfn "Name: %s" ("User" + (subReplyInfo.TargetUserID.ToString()))
    printf "Subscribe To: "
    for id in subReplyInfo.Subscriber do
        printf "User%i " id
    printf "\nPublish To: "
    for id in subReplyInfo.Publisher do
        printf "User%i " id
    printfn "\n"
    printfn "[%s] Query Subscribe done" nodeName

let queryCallback (nodeName) = fun (msg:MessageEventArgs) ->
    let  jsonMsg = JsonValue.Parse(msg.Data)
    let  reqType = jsonMsg?Type.AsString()
    if reqType = "ShowTweet" then printTweet (msg.Data)
    else if reqType = "ShowSub" then printSubscribe (msg.Data) (nodeName)
    else
        let isSuccess = if (jsonMsg?Status.AsString() = "Success") then (true) else (false)
        if isSuccess then printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
        else printfn "[%s] [Error] %s" nodeName (jsonMsg?Desc.AsString())




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


let sendRequestMsgToServer (msg:string, reqType, wssDB:Dictionary<string,WebSocket>, nodeName) =
    if not (wssDB.[reqType].IsAlive) then
        printfn "[%s] Unable to \"%s\", please connect to the server first..." nodeName reqType
    else 
        wssDB.[reqType].Send(msg)  