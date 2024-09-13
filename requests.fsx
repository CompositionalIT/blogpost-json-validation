#r "nuget: FsHttp"

open System.Net
open FsHttp
open System

let baseUrl = "http://localhost:5000"

http {
    POST baseUrl
    body

    jsonSerialize
        {|
        //Addressee = "Barry"
        // Tone = "Casual"
        |}
}
|> Request.send
