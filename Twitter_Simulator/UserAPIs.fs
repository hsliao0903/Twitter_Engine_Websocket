module UserAPIs

open System
open System.Security.Cryptography
open Authentication
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
    if (not wssDB.["SendTweet"].IsAlive) then (wssDB.["SendTweet"].Connect())
    if (not wssDB.["Retweet"].IsAlive) then (wssDB.["Retweet"].Connect())
    if (not wssDB.["Subscribe"].IsAlive) then (wssDB.["Subscribe"].Connect())
    if (not wssDB.["Disconnect"].IsAlive) then (wssDB.["Disconnect"].Connect())
    if (not wssDB.["QueryHistory"].IsAlive) then (wssDB.["QueryHistory"].Connect())
    if (not wssDB.["QueryMention"].IsAlive) then (wssDB.["QueryMention"].Connect())
    if (not wssDB.["QueryTag"].IsAlive) then (wssDB.["QueryTag"].Connect())
    if (not wssDB.["QuerySubscribe"].IsAlive) then (wssDB.["QuerySubscribe"].Connect())

let disableWss (wssDB:Dictionary<string, WebSocket>) =
    if (wssDB.["SendTweet"].IsAlive) then (wssDB.["SendTweet"].Close())
    if (wssDB.["Retweet"].IsAlive) then (wssDB.["Retweet"].Close())
    if (wssDB.["Subscribe"].IsAlive) then (wssDB.["Subscribe"].Close())
    if (wssDB.["Disconnect"].IsAlive) then (wssDB.["Disconnect"].Close())
    if (wssDB.["QueryHistory"].IsAlive) then (wssDB.["QueryHistory"].Close())
    if (wssDB.["QueryMention"].IsAlive) then (wssDB.["QueryMention"].Close())
    if (wssDB.["QueryTag"].IsAlive) then (wssDB.["QueryTag"].Close())
    if (wssDB.["QuerySubscribe"].IsAlive) then (wssDB.["QuerySubscribe"].Close())

// 
// Websocket OnMessage Callback Functions
// 

// Register reuqest callback
let regCallback (nodeName, wssDB:Dictionary<string,WebSocket>, isSimulation:bool) = fun (msg:MessageEventArgs) ->
    let replyInfo = (Json.deserialize<RegReply> msg.Data)
    let isSuccess = if (replyInfo.Status = "Success") then (true) else (false)

    if isSuccess then
        enableWss (wssDB)
        if isSimulation then 
            printfn "[%s] User \"%s\" registered and auto login successfully" nodeName (replyInfo.Desc.Value)
        else 
        serverPublicKey <- replyInfo.ServerPublicKey
        if isAuthDebug then
            printfn "\n\nReceive the server's ECDH public key: %A" serverPublicKey
        isUserModeLoginSuccess <- Success
    else
        if isSimulation then printfn "[%s] Register failed!\n" nodeName
        else isUserModeLoginSuccess <- Fail
    // Close the session for /register
    wssDB.["Register"].Close()

let connectCallback (nodeID, wssDB:Dictionary<string,WebSocket>, ecdh: ECDiffieHellman) = fun (msg:MessageEventArgs) ->
    let replyInfo = (Json.deserialize<ConnectReply> msg.Data)
    match replyInfo.Status with
    | "Success" -> 
        enableWss (wssDB)
        
        wssDB.["Connect"].Close()
        if isAuthDebug then
            printfn "[User%i] Server confirms our authentication, successfully login!" nodeID
        (* Automatically query the history tweets of the connected user *)
        let (queryMsg:QueryInfo) = {
            ReqType = "QueryHistory" ;
            UserID = (replyInfo.Desc.Value|> int) ;
            Tag = "" ;
        }
        wssDB.["QueryHistory"].Send(Json.serialize queryMsg)
        isUserModeLoginSuccess <- Success
    | "Auth" -> 
        let challenge = replyInfo.Authentication |> Convert.FromBase64String
        if isAuthDebug then
            printfn "Receicve a challenge from Server: %A" replyInfo.Authentication
            printfn "Now, add a time padding to the challenge and then digital signs it...\n"
        let signature = getSignature challenge ecdh
        if isAuthDebug then 
            printfn "The signature after signing with User's private key : %A" signature
        let (authMsg:ConnectInfo) = {
            UserID = replyInfo.Desc.Value|> int;
            ReqType = "Auth";
            Signature = signature;
        }
        wssDB.["Connect"].Send(Json.serialize authMsg)
    | _ ->
        isUserModeLoginSuccess <- Fail
        printBanner (sprintf "Faild to connect and login for UserID: %i\nError msg: %A" nodeID (replyInfo.Desc.Value))
        wssDB.["Connect"].Close()

let disconnectCallback (nodeName, wssDB:Dictionary<string,WebSocket>) = fun (msg:MessageEventArgs) ->
    disableWss (wssDB)
    isUserModeLoginSuccess <- Success


let replyCallback (nodeName) = fun (msg:MessageEventArgs) ->
    let replyInfo = (Json.deserialize<ReplyInfo> msg.Data)
    let isSuccess = if (replyInfo.Status = "Success") then (true) else (false)
    if isSuccess then
        isUserModeLoginSuccess <- Success
        //printfn "[%s] %s" nodeName (replyInfo.Desc.Value)
        printBanner (sprintf "[%s] %s" nodeName (replyInfo.Desc.Value))
    else 
        isUserModeLoginSuccess <- Fail
        printBanner (sprintf "[%s] [Error]\n%s" nodeName (replyInfo.Desc.Value))

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
    printBanner (sprintf "[%s] Finish to show User%i's subscribe status" nodeName subReplyInfo.TargetUserID)

let queryCallback (nodeName) = fun (msg:MessageEventArgs) ->
    let  jsonMsg = JsonValue.Parse(msg.Data)
    let  reqType = jsonMsg?Type.AsString()
    if reqType = "ShowTweet" then
        printTweet (msg.Data)
    else if reqType = "ShowSub" then 
        printSubscribe (msg.Data) (nodeName)
    else
        let isSuccess = if (jsonMsg?Status.AsString() = "Success") then (true) else (false)
        if isSuccess then 
            isUserModeLoginSuccess <- Success
            printBanner (sprintf "[%s]\n%s" nodeName (jsonMsg?Desc.AsString()))
        else 
            isUserModeLoginSuccess <- Fail
            printBanner (sprintf "[%s]\n%s" nodeName (jsonMsg?Desc.AsString()))




// 
// Client Actor Node Helper Functinos
// 

let sendRegMsgToServer (msg:string, isSimulation, wssReg:WebSocket, nodeID, publicKey) =
    wssReg.Connect()
    if isSimulation then
        let regMsg:RegJson = { 
            ReqType = "Register" ; 
            UserID = nodeID ; 
            UserName = "User"+ (nodeID.ToString()) ; 
            PublicKey = publicKey ;
        }
        let data = (Json.serialize regMsg)
        wssReg.Send(data)
    else
        //wssReg.Send(msg)
        let message = Json.deserialize<RegJson> msg
        let regMsg:RegJson = { 
            ReqType = message.ReqType; 
            UserID = message.UserID; 
            UserName = message.UserName ; 
            PublicKey = publicKey ;
        }
        let data = (Json.serialize regMsg)
        wssReg.Send(data)
   

let sendRequestMsgToServer (msg:string, reqType, wssDB:Dictionary<string,WebSocket>, nodeName) =
    if not (wssDB.[reqType].IsAlive) then
        if reqType = "Disconnect" then
            wssDB.[reqType].Connect()
            wssDB.[reqType].Send(msg)
            printBanner (sprintf "[%s]\nDisconnect from the server..." nodeName)
            isUserModeLoginSuccess <- SessionTimeout
            // printBanner (sprintf "[%s] Unable to \"%s\", session timeout!\n Please disconnect and then reconnect to the server..." nodeName reqType)
        else
            isUserModeLoginSuccess <- SessionTimeout
    else 
        wssDB.[reqType].Send(msg)

let sendTweetToServer (msg:string, ws:WebSocket, nodeName, ecdh: ECDiffieHellman) =
    if not (ws.IsAlive) then
        isUserModeLoginSuccess <- SessionTimeout        
    else 
        let key = getSharedSecretKey ecdh serverPublicKey

        let signature = getHMACSignature msg key
        if isAuthDebug then
            printfn "Generate shared secret key with server with user's private key and server's public key:"
            printfn "Shared secret key (it won't send to the server):\n %A" (key|>Convert.ToBase64String)
            printfn "Origin message: %A" msg
            printfn "HMAC sign the org message with shared secret key before sending to server"
            printfn "HMAC signatrue: %A" signature
        let (signedMsg:SignedTweet) = {
            UnsignedJson = msg
            HMACSignature = signature
        }
        let data = (Json.serialize signedMsg)
        ws.Send(data)