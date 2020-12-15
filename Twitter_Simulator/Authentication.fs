module Authentication

open System
open System.Security.Cryptography


let stringToBytes (str: string) = 
    Text.Encoding.UTF8.GetBytes str

///////////////////////////////////////////////
/// Challenge Algorithm Functions
/////////////////////////////////////////////// 

let generateChallenge =
    use rng = RNGCryptoServiceProvider.Create()
    let challenge = Array.zeroCreate<byte> 32
    rng.GetBytes challenge
    challenge |> Convert.ToBase64String

let addTimePadding (message: byte[]) =
    let padding = DateTimeOffset.UtcNow.ToUnixTimeSeconds() |> BitConverter.GetBytes
    Array.concat [|message; padding|]

let getHashed (message: byte[]) = 
    message |> SHA256.HashData 

let getECPublicKey (pub:byte[]) =
    let ecdh = ECDiffieHellman.Create()
    let size = ecdh.KeySize
    ecdh.ImportSubjectPublicKeyInfo((System.ReadOnlySpan pub), (ref size))
    ecdh.ExportParameters(false)

let getSignature (message: byte[]) (ecdh: ECDiffieHellman) =
    let ecdsa = ecdh.ExportParameters(true) |> ECDsa.Create
    let hasedMessage = message |> addTimePadding |> getHashed
    ecdsa.SignData(hasedMessage, HashAlgorithmName.SHA256) |> Convert.ToBase64String

///////////////////////////////////////////////
/// DH & HMAC Functions
/////////////////////////////////////////////// 

let getSharedSecretKey (clientECDH: ECDiffieHellman) (publicKey: String) = 
    let pub = publicKey |> Convert.FromBase64String
    let size = clientECDH.KeySize
    let temp = ECDiffieHellman.Create()
    temp.ImportSubjectPublicKeyInfo((System.ReadOnlySpan pub), (ref size))
    clientECDH.DeriveKeyMaterial(temp.PublicKey)

let getHMACSignature (jsonMessage: string) (sharedSecretKey: byte[]) =    
    use hmac = new HMACSHA1(sharedSecretKey)
    jsonMessage |> stringToBytes |> hmac.ComputeHash |> Convert.ToBase64String

