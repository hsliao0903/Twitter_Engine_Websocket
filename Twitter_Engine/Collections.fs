module TwitterServerCollections

open System
open System.Collections.Generic
open System.Security.Cryptography
open Akka.Actor
open Akka.FSharp

open Authentication

(* Different API request JSON message structures *)

type ReplyInfo = {
    ReqType : string
    Type : string
    Status : string
    Desc : string option
}

type RegInfo = {
    ReqType : string
    UserID : int
    UserName : string
    PublicKey : string
}

type RegReply = {
    ReqType : string
    Type : string
    Status : string
    ServerPublicKey : string
    Desc: string option
}

type SignedTweet = {
    UnsignedJson : string
    HMACSignature: string
}

type TweetInfo = {
    ReqType : string
    UserID : int
    TweetID : string
    Time : DateTime
    Content : string
    Tag : string
    Mention : int
    RetweetTimes : int
}

type TweetReply = {
    ReqType : string
    Type : string
    Status : int
    TweetInfo : TweetInfo
}

type SubInfo = {
    ReqType : string
    UserID : int 
    PublisherID : int
}

type SubReply = {
    ReqType : string
    Type : string
    TargetUserID : int
    Subscriber : int[]
    Publisher : int[]
}

type ConnectInfo = {
    ReqType : string
    UserID : int
    Signature: string
}

type ConnectReply = {
    ReqType: string
    Type: string
    Status: string
    Authentication: string
    Desc: string option
}

type QueryInfo = {
    ReqType : string
    UserID : int
    Tag : string
}

type RetweetInfo = {
    ReqType: string
    UserID : int
    TargetUserID : int
    RetweetID : string
}

type KeyInfo = {
    UserPublicKey: String
    SharedSecretKey: String
    ServerECDH: ECDiffieHellman
}

(* Data Collections to Store Client informations *)
(* userID, info of user registration *)
let regMap = new Dictionary<int, RegInfo>()
(* tweetID, info of the tweet *)
let tweetMap = new Dictionary<string, TweetInfo>()
(* userID, list of tweetID*)
let historyMap = new Dictionary<int, List<string>>()
(* tag, list of tweetID *)
let tagMap = new Dictionary<string, List<string>>()
(* userID, list of subsriber's userID *)
let pubMap = new Dictionary<int, List<int>>()
(* userID, list of publisher's userID *)
let subMap = new Dictionary<int, List<int>>()
(* userID, list of tweetID that mentions the user *)
let mentionMap = new Dictionary<int, List<string>>()
(* userID, info of keys *)
let keyMap = new Dictionary<int, KeyInfo>()

let challengeCache = new Dictionary<int, String>()


(* Spawn Actors Helpter Function *)
let actorOfSink f = actorOf f

(* Helper Functions for access storage data structure *)

let isValidUser userID = 
    (regMap.ContainsKey(userID)) 

let challengeCaching (userID: int) (challenge: string) =
    async{
        challengeCache.Add(userID, challenge)
        do! Async.Sleep 1000
        printfn "Challenge expired for: %A\n[timestamp] %A " challenge (DateTime.Now)
        challengeCache.Remove(userID) |> ignore
    }
    


// let regMapAdder (userID, info:RegInfo) =
//     regMap.Add(userID, info)
// let regMapAdderActorRef =
//     actorOfSink regMapAdder |> spawn system "regMap-Adder"

let updateRegDB (newInfo:RegInfo) =
    let userID = newInfo.UserID
    if not (regMap.ContainsKey(userID)) then
        //regMapAdderActorRef <! (userID, newInfo)
        regMap.Add(userID, newInfo)
        "Success"
    else
        "Fail"

let updateKeyDB (newInfo:RegInfo) (serverECDH: ECDiffieHellman)=
    keyMap.Add(
        newInfo.UserID,
        {
            UserPublicKey = newInfo.PublicKey;
            SharedSecretKey = 
                (getSharedSecretKey serverECDH newInfo.PublicKey) |> Convert.ToBase64String;
            ServerECDH = serverECDH;
        })

let updateHistoryDB userID tweetID =
    (* Check if it is the very first Tweet for a user, 
        if not, initialize it, if yes, add it to the list *)
    if userID >= 0 && (isValidUser userID) then        
        if not (historyMap.ContainsKey(userID)) then
            let newList = new List<string>()
            newList.Add(tweetID)
            historyMap.Add(userID, newList)
        else
            (* No duplicate tweetID in one's history *)
            if not (historyMap.[userID].Contains(tweetID)) then
                (historyMap.[userID]).Add(tweetID)
    
let updateTagDB tag tweetID = 
    (* Update Tag database *)
    if tag <> "" && tag.[0] = '#' then
        if not (tagMap.ContainsKey(tag)) then
            let newList = new List<string>()
            newList.Add(tweetID)
            tagMap.Add(tag, newList)
        else
            (tagMap.[tag]).Add(tweetID)

let updatePubSubDB publisherID subscriberID = 
    let mutable isFail = false
    (* Don't allow users to subscribe themselves *)
    if publisherID <> subscriberID && (isValidUser publisherID) && (isValidUser subscriberID) then
        (* pubMap:  Publisher : list of subscribers  *)
        if not (pubMap.ContainsKey(publisherID)) then
            let newList = new List<int>()
            newList.Add(subscriberID)
            pubMap.Add(publisherID, newList)
        else
            if not ((pubMap.[publisherID]).Contains(subscriberID)) then
                (pubMap.[publisherID]).Add(subscriberID)
            else
                isFail <- true

        (* pubMap:  Subscriber : list of Publishers *)
        if not (subMap.ContainsKey(subscriberID)) then
            let newList = new List<int>()
            newList.Add(publisherID)
            subMap.Add(subscriberID, newList)
        else
            if not ((subMap.[subscriberID]).Contains(publisherID)) then
                (subMap.[subscriberID]).Add(publisherID)
            else
                isFail <- true
        if isFail then
            "Fail"
        else
            "Success"
    else
        "Fail"

let updateMentionDB userID tweetID =
    (* Make suer the mentino exist some valid userID *)
    if userID >= 0 && (isValidUser userID) then
       if not (mentionMap.ContainsKey(userID)) then
            let newList = new List<string>()
            newList.Add(tweetID)
            mentionMap.Add(userID, newList)
        else
            (mentionMap.[userID]).Add(tweetID)

let updateTweetDB (newInfo:TweetInfo) =
    let tweetID = newInfo.TweetID
    let userID = newInfo.UserID
    let tag = newInfo.Tag
    let mention = newInfo.Mention
    
    (* Add the new Tweet info Tweet DB *)
    (* Assume that the tweetID is unique *)
    tweetMap.Add(tweetID, newInfo)
    (* Update the history DB for the user when send this Tweet *)
    updateHistoryDB userID tweetID
    (* Update the tag DB if this tweet has a tag *)
    updateTagDB tag tweetID
    
    (* If the user has mentioned any user, update his history*) 
    updateMentionDB mention tweetID
    updateHistoryDB mention tweetID

    (* If the user has subscribers update their historyDB *)
    if (pubMap.ContainsKey(userID)) then
        for subscriberID in (pubMap.[userID]) do
            (* If the tweet mentions it's author's subscriber, skip it to avoid duplicate tweetID in history *)
            //if mention <> subscriberID then
            updateHistoryDB subscriberID tweetID

(* userID: the user who would like to retweet *)
let updateRetweet userID (orgTweetInfo:TweetInfo) =
    let newTweetInfo:TweetInfo = {
        ReqType = orgTweetInfo.ReqType ;
        UserID  = orgTweetInfo.UserID ;
        TweetID = orgTweetInfo.TweetID ;
        Time = orgTweetInfo.Time ;
        Content = orgTweetInfo.Content ;
        Tag = orgTweetInfo.Tag ;
        Mention = orgTweetInfo.Mention ;
        RetweetTimes = (orgTweetInfo.RetweetTimes+1) ;
    }
    (* Increase the retweet times by one *)
    tweetMap.[orgTweetInfo.TweetID] <- newTweetInfo

    (* Add to the history *)
    updateHistoryDB userID (orgTweetInfo.TweetID)
   
    (* If the user has subscribers update their historyDB *)
    if (pubMap.ContainsKey(userID)) then
        for subscriberID in (pubMap.[userID]) do
            updateHistoryDB subscriberID (orgTweetInfo.TweetID)         

let assignTweetID (orgTweetInfo:TweetInfo) =
    let newTweetInfo:TweetInfo = {
        ReqType = orgTweetInfo.ReqType ;
        UserID  = orgTweetInfo.UserID ;
        //TweetID = totalTweets.ToString() ; // assign new tweetID according to total tweet counts
        TweetID = (tweetMap.Count + 1).ToString() ;
        Time = orgTweetInfo.Time ;
        Content = orgTweetInfo.Content ;
        Tag = orgTweetInfo.Tag ;
        Mention = orgTweetInfo.Mention ;
        RetweetTimes = orgTweetInfo.RetweetTimes ;
    }
    newTweetInfo




// --------------------------- DB collections ---------------------------
// regMap tweetMap historyMap tagMap mentionMap subMap pubMap
//-----------------------------------------------------------------------
let getTopID (subpubMap:Dictionary<int, List<int>>) = 
    let mutable maxCount = 0
    let mutable topID = -1 
    for entry in subpubMap do
        if entry.Value.Count > maxCount then
            maxCount <- (entry.Value.Count)
            topID <- entry.Key
    topID   
let getTopTag (tagDB:Dictionary<string, List<string>>) =
    let mutable maxCount = 0
    let mutable topTag = ""
    for entry in tagDB do
        if entry.Value.Count > maxCount then
            maxCount <- (entry.Value.Count)
            topTag <- entry.Key
    topTag       
let getTopMention (mentionDB:Dictionary<int, List<string>>) =
    let mutable maxCount = 0
    let mutable topMen = -1
    for entry in mentionMap do
        if entry.Value.Count > maxCount then
            maxCount <- (entry.Value.Count)
            topMen <- entry.Key
    topMen
let getTopRetweet (tweetDB:Dictionary<string, TweetInfo>) =
    let mutable maxCount = 0
    let mutable topRetweet = ""
    for entry in tweetMap do
        if entry.Value.RetweetTimes > maxCount then
            maxCount <- (entry.Value.RetweetTimes)
            topRetweet <- entry.Key
    topRetweet

let showDBStatus (showStatusMode:int) _ =
    if showStatusMode = 1 then
        let topPublisher = getTopID pubMap
        let topSubscriber = getTopID subMap
        let topTag = getTopTag tagMap
        let topMention = getTopMention mentionMap
        let topRetweet = getTopRetweet tweetMap
                
        printfn "\n---------- DB Status ---------------------"
        printfn "Total Registered Users: %i" (regMap.Keys.Count)
        printfn "Total Tweets in DB: %i" (tweetMap.Keys.Count)
        if topRetweet <> "" then
            printfn "Top retweeted Tweet: %s (%i times)" topRetweet (tweetMap.[topRetweet].RetweetTimes)
        if topTag <> "" then
            printfn "Total different kinds of Tags: %i" (tagMap.Keys.Count)
            printfn "Top used Tag: %s (%i times)" topTag (tagMap.[topTag].Count)
        if topMention >= 0 then
            printfn "Top mentioned User: User%i (%i times)" topMention (mentionMap.[topMention].Count)
        if topPublisher >= 0 then
            printfn "Top Publisher: %i (%i subscribers)" topPublisher (pubMap.[topPublisher].Count)
        if topSubscriber >= 0 then
            printfn "Top Subscriber: %i (%i subscribes)" topSubscriber (subMap.[topSubscriber].Count)
        printfn "------------------------------------------\n"