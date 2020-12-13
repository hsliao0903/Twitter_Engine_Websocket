open System
open System.Security.Cryptography
open System.Globalization
open System.Text
open Akka.Actor
open Akka.FSharp
open System.Diagnostics


(* For Json Libraries *)
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json

open Simulator
open Message
open UserInterface

(* Websocket Interface *)
open WebSocketSharp


 (* Actor System Configuration Settings (Locaol Side) *)
let config =
    Configuration.parse
        @"akka {
            log-dead-letters = off
            log-dead-letters-during-shutdown = off
            log-config-on-start = off
            actor.provider = remote
            remote.helios.tcp {
                hostname = localhost
                port = 0
            }
        }"

(* Some globalal variables *)
let system = System.create "Simulator" config
let serverNode = system.ActorSelection("akka.tcp://TwitterEngine@localhost:9001/user/TWServer")
let globalTimer = Stopwatch()
let mutable isSimulation = false



(* Client Node Actor*)
let clientActorNode (clientMailbox:Actor<string>) =
    let mutable nodeName = "User" + clientMailbox.Self.Path.Name
    let mutable nodeID = 
        match (Int32.TryParse(clientMailbox.Self.Path.Name)) with
        | (true, value) -> value
        | (false, _) -> 0
    
    let nodeSelfRef = clientMailbox.Self
    
    (* User have to connect (online) to server first before using twitter API, register API has no this kind of limit *)
    let mutable isOnline = false
    let mutable isDebug = false // developer use, break this limit
    let mutable isOffline = true

    (* Need a query lock to make sure there is other query request until the last query has done*)
    (* If a new query request comes, set it to true, until the server replies query seccess in reply message *)
    let mutable isQuerying = false

    let rec loop() = actor {
        let! (message: string) = clientMailbox.Receive()
        let  jsonMsg = JsonValue.Parse(message)
        let  reqType = jsonMsg?ReqType.AsString()
        isOffline <- (not isOnline) && (not isDebug)
        match reqType with
            | "Register" ->
                if isSimulation then
                    (* Example JSON message for register API *)
                    let regMsg:RegJson = { 
                        ReqType = reqType ; 
                        UserID = nodeID ; 
                        UserName = nodeName ; 
                        PublicKey = Some (nodeName+"Key") ;
                    }
                    serverNode <! (Json.serialize regMsg)
                else
                    serverNode <! message
                


            | "SendTweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                else   

                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message

            | "Retweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                else

                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message



            | "Subscribe" ->
                if isOffline then
                    printfn "[%s] Subscribe failed, please connect to Twitter server first" nodeName
                else

                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message
                


            | "Connect" ->
                if isOnline then
                    if isSimulation then
                        nodeSelfRef <! """{"ReqType":"QueryHistory", "UserID":"""+"\""+ nodeID.ToString() + "\"}"
                    else
                        
                        serverNode <! message
                else
                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message
                
    

            | "Disconnect" ->
                isOnline <- false

                if isSimulation then
                    serverNode <! message
                else

                    serverNode <! message
                


            | "QueryHistory" | "QuerySubscribe" | "QueryMention" | "QueryTag" ->
                if isOffline then
                    printfn "[%s] Query failed, please connect to Twitter server first" nodeName
                else
                    if isQuerying then
                        printfn "[%s] Query failed, please wait until the last query is done" nodeName
                    else
                        (* Set querying lock avoiding concurrent queries *)
                        isQuerying <- true

                        if reqType = "QueryTag" then
                            if isSimulation then
                                serverNode <! message
                            else
                                serverNode <! message
                        else
                            if isSimulation then
                                serverNode <! message
                            else
                                serverNode <! message
                    

                
            (* Deal with all reply messages  *)
            | "Reply" ->
                let replyType = jsonMsg?Type.AsString()
                match replyType with
                    | "Register" ->

                        let status = jsonMsg?Status.AsString()
                        let registerUserID = jsonMsg?Desc.AsString() |> int
                        if status = "Success" then
                            if isSimulation then 
                                printfn "[%s] Successfully registered" nodeName
                            else
                                isUserModeLoginSuccess <- Success
                            (* If the user successfully registered, connect to the server automatically *)
                            let (connectMsg:ConnectInfo) = {
                                ReqType = "Connect" ;
                                UserID = registerUserID ;
                            }
                            serverNode <! (Json.serialize connectMsg)
                            globalTimer.Restart()
                        else
                            if isSimulation then 
                                printfn "[%s] Register failed!\n\t(this userID might have already registered before)" nodeName
                            else
                                isUserModeLoginSuccess <- Fail
                        

                    | "Subscribe" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] Subscirbe done!" nodeName
                        else
                            printfn "[%s] Subscribe failed!" nodeName

                    | "SendTweet" ->
                        
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())

                    | "Connect" ->
                     
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isOnline <- true
                            if isSimulation then 
                                printfn "[%s] User%s successfully connected to server" nodeName (jsonMsg?Desc.AsString())
                            else
                                isUserModeLoginSuccess <- Success
                            (* Automatically query the history tweets of the connected user *)
                            let (queryMsg:QueryInfo) = {
                                ReqType = "QueryHistory" ;
                                UserID = (jsonMsg?Desc.AsString()|> int) ;
                                Tag = "" ;
                            }
                            serverNode <! (Json.serialize queryMsg)
                            globalTimer.Restart()
                        else
                            if isSimulation then 
                                printfn "[%s] Connection failed, %s" nodeName (jsonMsg?Desc.AsString())
                            else
                                isUserModeLoginSuccess <- Fail


                    | "Disconnect" ->
                  
                        if isSimulation then 
                            printfn "[%s] User%s disconnected from the server" nodeName (jsonMsg?Desc.AsString())
                        else
                            isUserModeLoginSuccess <- Success
                    | "QueryHistory" ->

                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isQuerying <- false
                            printfn "\n[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else if status = "NoTweet" then
                            isQuerying <- false
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else
                            printfn "[%s] Something Wrong with Querying History" nodeName

                    | "ShowTweet" ->
                        (* Don't print out any tweet on console if it is in simulation mode *)
                        if not isSimulation then
                            let tweetReplyInfo = (Json.deserialize<TweetReply> message)
                            let tweetInfo = tweetReplyInfo.TweetInfo
                            printfn "\n------------------------------------"
                            printfn "Index: %i      Time: %s" (tweetReplyInfo.Status) (tweetInfo.Time.ToString())
                            printfn "Author: User%i" (tweetInfo.UserID)
                            printfn "Content: {%s}\n%s  @User%i  Retweet times: %i" (tweetInfo.Content) (tweetInfo.Tag) (tweetInfo.Mention) (tweetInfo.RetweetTimes)
                            printfn "TID: %s" (tweetInfo.TweetID)

                    | "ShowSub" ->
                        isQuerying <- false
                        if not isSimulation then
                            let subReplyInfo = (Json.deserialize<SubReply> message)
                            
                            printfn "\n------------------------------------"
                            printfn "Name: %s" ("User" + subReplyInfo.TargetUserID.ToString())
                            printf "Subscribe To: "
                            for id in subReplyInfo.Subscriber do
                                printf "User%i " id
                            printf "\nPublish To: "
                            for id in subReplyInfo.Publisher do
                                printf "User%i " id
                            printfn "\n"
                            printfn "[%s] Query Subscribe done" nodeName
                            

                    | _ ->
                        printfn "[%s] Unhandled Reply Message" nodeName

            | "UserModeOn" ->
                let curUserID = jsonMsg?CurUserID.AsInteger()
                nodeID <- curUserID
                nodeName <- "User" + curUserID.ToString()

            | _ ->
                printfn "Client node \"%s\" received unknown message \"%s\"" nodeName reqType
                Environment.Exit 1
         
        return! loop()
    }
    loop()



[<EntryPoint>]
let main argv =
    try
        globalTimer.Start()
        (* dotnet run [simulate | user | debug] *)
        let programMode = argv.[0]

        if programMode = "user" then
            (* Create a terminal actor node for user mode *)
            
            let terminalRef = spawn system "TerminalNode" (clientActorNode)
            startUserInterface terminalRef

        else if programMode = "simulate" then
            getSimualtionParamFromUser()
            isSimulation <- true
            startSimulation system globalTimer clientActorNode

        else if programMode = "debug" then
            isSimulation <- true
            printfn "\n\n[Debug Mode]\n"
            // use default simulation parameters
            startSimulation system globalTimer clientActorNode
        else
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n\t1. dotnet run simulate\n\t2. dotnet run user\n\t3. dotnet run debug\n"
            Environment.Exit 1

          
    with | :? IndexOutOfRangeException ->
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n1. dotnet run simulate\n2. dotnet run user\n\n"

         | :? FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 