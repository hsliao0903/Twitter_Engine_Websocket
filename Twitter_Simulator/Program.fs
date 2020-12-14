open System
open System.Security.Cryptography
open System.Globalization
open System.Text
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp
open System.Diagnostics
open UserAPIs

(* For Json Libraries *)
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json

open Simulator
open Message
open UserInterface

(* Websocket Interface *)
open WebSocketSharp

let system = ActorSystem.Create("UserInterface")
let serverWebsockAddr = "ws://localhost:9001"
let globalTimer = Stopwatch()







(* Client Node Actor*)
let clientActorNode (isSimulation) (clientMailbox:Actor<string>) =
    let mutable nodeName = "User" + clientMailbox.Self.Path.Name
    let mutable nodeID = 
        match (Int32.TryParse(clientMailbox.Self.Path.Name)) with
        | (true, value) -> value
        | (false, _) -> 0
    

    let wssDB = createWebsocketDB (serverWebsockAddr)
    (wssDB.["Register"]).OnMessage.Add(regCallback (nodeName, wssDB, isSimulation))
    (wssDB.["SendTweet"]).OnMessage.Add(replyCallback (nodeName))
    (wssDB.["Retweet"]).OnMessage.Add(replyCallback (nodeName))
    (wssDB.["Subscribe"]).OnMessage.Add(replyCallback (nodeName))
    (wssDB.["QueryHistory"]).OnMessage.Add(queryCallback (nodeName))
    (wssDB.["QueryMention"]).OnMessage.Add(queryCallback (nodeName))
    (wssDB.["QueryTag"]).OnMessage.Add(queryCallback (nodeName))
    (wssDB.["QuerySubscribe"]).OnMessage.Add(queryCallback (nodeName))
    (wssDB.["Disconnect"]).OnMessage.Add(disconnectCallback (nodeName, wssDB))
    (wssDB.["Connect"]).OnMessage.Add(connectCallback (nodeName, wssDB))


    let rec loop() = actor {
        let! (message: string) = clientMailbox.Receive()
        let  jsonMsg = JsonValue.Parse(message)
        let  reqType = jsonMsg?ReqType.AsString()
        match reqType with
            | "Register" ->
                sendRegMsgToServer (message,isSimulation, wssDB.[reqType], nodeID)

            | "SendTweet" | "Retweet" | "Subscribe"
            | "QueryHistory" | "QueryMention" | "QueryTag" | "QuerySubscribe" 
            | "Disconnect" ->
                sendRequestMsgToServer (message, reqType, wssDB, nodeName)        

            | "Connect" ->
                let wssCon = wssDB.["Connect"]
                wssCon.Connect()
                // TODO: send private key to this call back
                // start authentication with server in this call back
                // Need New Form of JSON that could encapsule origin JSON
                wssCon.Send(message)

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
            
            let terminalRef = spawn system "-Terminal" (clientActorNode false)
            startUserInterface terminalRef

        else if programMode = "simulate" then
            getSimualtionParamFromUser()
            startSimulation system globalTimer (clientActorNode true)

        else if programMode = "debug" then
            printfn "\n\n[Debug Mode]\n"
            // use default simulation parameters
            startSimulation system globalTimer (clientActorNode true)
        else
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n\t1. dotnet run simulate\n\t2. dotnet run user\n\t3. dotnet run debug\n"
            Environment.Exit 1

          
    with | :? IndexOutOfRangeException ->
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n1. dotnet run simulate\n2. dotnet run user\n\n"

         | :? FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 