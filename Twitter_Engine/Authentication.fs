module Authentication

open System
open System.Security.Cryptography

let mutable isAuthDebug = false

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
    
let verifySignature (challenge: string) (signature: string) (publicKey: string) =     
    let ans = challenge |> Convert.FromBase64String |> addTimePadding |> getHashed
    let sign = signature.[0 .. 255] |> Convert.FromBase64String

    let pub = publicKey |> Convert.FromBase64String
   
    let ecdsaParams = pub |> getECPublicKey
    let ecdsa = ECDsa.Create(ecdsaParams)
    ecdsa.VerifyData(ans, sign, HashAlgorithmName.SHA256)

///////////////////////////////////////////////
/// DH & HMAC Functions
/////////////////////////////////////////////// 

let getSharedSecretKey (serverECDH: ECDiffieHellman) (publicKey: String) = 
    let pub = publicKey |> Convert.FromBase64String
    let size = serverECDH.KeySize
    let ecdh = ECDiffieHellman.Create()
    ecdh.ImportSubjectPublicKeyInfo((System.ReadOnlySpan pub), (ref size))
    serverECDH.DeriveKeyMaterial(ecdh.PublicKey)

// let getHMACSignature (jsonMessage: string) (sharedSecretKey: byte[]) =
//     use hmac = new HMACSHA1(sharedSecretKey)
//     jsonMessage |> stringToBytes |> hmac.ComputeHash

let verifyHMAC (jsonMessage:string) (signature: string) (sharedSecretKey: byte[]) =
    use hmac = new HMACSHA1(sharedSecretKey)
    if isAuthDebug then
        printfn "Verifying the HMAC signature for this Json message with shared secret key..."
    let computedSignature = jsonMessage |> stringToBytes |> hmac.ComputeHash |> Convert.ToBase64String 
    if isAuthDebug then
        printfn "computed HMAC signature on Server side using shared secret key\n(shared secret key is computed by server's private key and user's public key)..."
        printfn "[Compare equivalent]\nJson msg HMAC: %A\nComputed HMAC: %A\n" signature computedSignature
    signature |> computedSignature.Equals
