module ActorOFDB

open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json
open System
open System.Text
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp
open System.Diagnostics
open TwitterServerCollections
open WebSocketSharp.Server

//
//
// Query Actor
//
//

type QueryActorMsg = 
| WsockToQActor of string * IActorRef * WebSocketSessionManager * string


type QueryWorkerMsg =
| QueryHistory of string * WebSocketSessionManager * string * string[]
| QueryTag of string * WebSocketSessionManager * string * string[]
| QueryMention of string * WebSocketSessionManager * string * string[]

let queryHistoryActor (serverMailbox:Actor<QueryActorMsg>) =
    let nodeName = serverMailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryActorMsg) = serverMailbox.Receive()
        match message with
        | WsockToQActor (msg, workerRef ,sessionManager, sid) ->
            let queryInfo = (Json.deserialize<QueryInfo> msg)
            let userID = queryInfo.UserID
            (* No any Tweet in history *)
            if not (historyMap.ContainsKey(userID)) then
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = queryInfo.ReqType ;
                    Status =  "NoTweet" ;
                    Desc =  Some "Query done, there is no any Tweet to show yet" ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)
            else
                workerRef <! QueryHistory (msg, sessionManager, sid, historyMap.[userID].ToArray())
        return! loop()
    }
    loop() 

let queryMentionActor (serverMailbox:Actor<QueryActorMsg>) =
    let nodeName = serverMailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryActorMsg) = serverMailbox.Receive()
        match message with
        | WsockToQActor (msg, workerRef ,sessionManager, sid) ->
            let queryInfo = (Json.deserialize<QueryInfo> msg)
            let userID = queryInfo.UserID
            (* No any Tweet in history *)
            if not (mentionMap.ContainsKey(userID)) then
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = queryInfo.ReqType ;
                    Status =  "NoTweet" ;
                    Desc =  Some "Query done, there is no any Tweet to show yet" ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)
            else
                workerRef <! QueryHistory (msg, sessionManager, sid, mentionMap.[userID].ToArray())
        return! loop()
    }
    loop() 

let queryTagActor (serverMailbox:Actor<QueryActorMsg>) =
    let nodeName = serverMailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryActorMsg) = serverMailbox.Receive()
        match message with
        | WsockToQActor (msg, workerRef ,sessionManager, sid) ->
            let queryInfo = (Json.deserialize<QueryInfo> msg)
            let tag = queryInfo.Tag
            (* No any Tweet in history *)
            if not (tagMap.ContainsKey(tag)) then
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = queryInfo.ReqType ;
                    Status =  "NoTweet" ;
                    Desc =  Some "Query done, there is no any Tweet to show yet" ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)
            else
                workerRef <! QueryHistory (msg, sessionManager, sid, tagMap.[tag].ToArray())
        return! loop()
    }
    loop() 

let querySubActor (serverMailbox:Actor<QueryActorMsg>) =
    let nodeName = serverMailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryActorMsg) = serverMailbox.Receive()
        match message with
        | WsockToQActor (msg, _ ,sessionManager, sid) ->
            let queryInfo = (Json.deserialize<QueryInfo> msg)
            let userID = queryInfo.UserID
            (* the user doesn't have any publisher subscripver information *)
            if not (subMap.ContainsKey(userID)) && not (pubMap.ContainsKey(userID))then
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = "QueryHistory" ;
                    Status =  "NoTweet" ;
                    Desc =  Some ("Query done, the user has no any subscribers or subscribes to others ")  ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)
            else if (subMap.ContainsKey(userID)) && not (pubMap.ContainsKey(userID))then
                let subReply:SubReply = {
                    ReqType = "Reply" ;
                    Type = "ShowSub" ;
                    TargetUserID = userID ;
                    Subscriber = subMap.[userID].ToArray() ;
                    Publisher = [||] ;
                }
                let data = (Json.serialize subReply)
                sessionManager.SendTo(data,sid)
            else if not (subMap.ContainsKey(userID)) && (pubMap.ContainsKey(userID))then
                let subReply:SubReply = {
                    ReqType = "Reply" ;
                    Type = "ShowSub" ;
                    TargetUserID = userID ;
                    Subscriber = [||] ;
                    Publisher = pubMap.[userID].ToArray() ;
                }
                let data = (Json.serialize subReply)
                sessionManager.SendTo(data,sid)
            else 
                let subReply:SubReply = {
                    ReqType = "Reply" ;
                    Type = "ShowSub" ;
                    TargetUserID = userID ;
                    Subscriber = subMap.[userID].ToArray() ;
                    Publisher = pubMap.[userID].ToArray() ;
                }
                let data = (Json.serialize subReply)
                sessionManager.SendTo(data,sid)     
        return! loop()
    }
    loop() 




//
//
// Query worker Actor (might of a bunch of workers)
//
//



let queryActorNode (mailbox:Actor<QueryWorkerMsg>) =
    let nodeName = "QueryActor " + mailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryWorkerMsg) = mailbox.Receive()
       
        match message with
            | QueryHistory (json, sessionManager, sid, tweetIDarray) ->
                let  jsonMsg = JsonValue.Parse(json)
                //printfn "[%s] %A" nodeName json
                let  userID = jsonMsg?UserID.AsInteger()
                
                (* send back all the tweets *)
                let mutable tweetCount = 0
                for tweetID in (tweetIDarray) do
                    if tweetMap.ContainsKey(tweetID) then
                        tweetCount <- tweetCount + 1
                        let tweetReply:TweetReply = {
                            ReqType = "Reply" ;
                            Type = "ShowTweet" ;
                            Status = tweetCount ;
                            TweetInfo = tweetMap.[tweetID] ;
                        }
                        let data = (Json.serialize tweetReply)
                        sessionManager.SendTo(data,sid)

                (* After sending ball all the history tweet, reply to sender *)
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = "QueryHistory" ;
                    Status =  "Success" ;
                    Desc =  Some "Query history Tweets done" ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)

            | QueryTag (json, sessionManager, sid, tweetIDarray) ->
                let  jsonMsg = JsonValue.Parse(json)
                //printfn "[%s] %A" nodeName json
                let tag = jsonMsg?Tag.AsString()
                (* send back all mentioned tweets *)
                let mutable tweetCount = 0
                for tweetID in tweetIDarray do
                    if tweetMap.ContainsKey(tweetID) then
                        tweetCount <- tweetCount + 1
                        
                        let tweetReply:TweetReply = {
                            ReqType = "Reply" ;
                            Type = "ShowTweet" ;
                            Status = tweetCount ;
                            TweetInfo = tweetMap.[tweetID] ;
                        }
                        let data = (Json.serialize tweetReply)
                        sessionManager.SendTo(data,sid)

                (* After sending back all the history tweet, reply to sender *)
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = "QueryHistory" ;
                    Status =  "Success" ;
                    Desc =  Some ("Query Tweets with "+tag+ " done") ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)

            | QueryMention (json, sessionManager, sid,tweetIDarray) ->
                let  jsonMsg = JsonValue.Parse(json)
                //printfn "[%s] %A" nodeName json
                let  userID = jsonMsg?UserID.AsInteger()
                let  reqType = jsonMsg?ReqType.AsString()
                (* send back all mentioned tweets *)
                let mutable tweetCount = 0
                for tweetID in (tweetIDarray) do
                    if tweetMap.ContainsKey(tweetID) then
                        tweetCount <- tweetCount + 1
                        let tweetReply:TweetReply = {
                            ReqType = "Reply" ;
                            Type = "ShowTweet" ;
                            Status = tweetCount ;
                            TweetInfo = tweetMap.[tweetID] ;
                        }
                        let data = (Json.serialize tweetReply)
                        sessionManager.SendTo(data,sid)

                (* After sending ball all the history tweet, reply to sender *)
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = "QueryHistory" ;
                    Status =  "Success" ;
                    Desc =  Some "Query mentioned Tweets done" ;
                }
                let data = (Json.serialize reply)
                sessionManager.SendTo(data,sid)
        return! loop()
    }
    loop()