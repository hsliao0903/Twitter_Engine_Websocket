module ServerWebSocketInterface

open System
open WebSocketSharp.Server
open UpdateDBActors


// let wss = WebSocketServer("ws://localhost:9001")



// type Register () =
//     inherit WebSocketBehavior()
//     override wssm.OnMessage message = 
//         printfn "Server rx: [Connect] %s" message.Data 
//         //serverActor <! ActorConnect (message.Data, x.Sessions, x.ID)



type Disconnect () =
    inherit WebSocketBehavior()
    override x.OnMessage message = 
        printfn "Server rx: [Disconnect] %s" message.Data 
        printfn "ID: %A" x.ID
        printfn "Session: %A" x.Sessions.IDs
        x.Send (message.Data + " [from server]")