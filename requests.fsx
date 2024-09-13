#r "nuget: FsHttp"

open FsHttp

let baseUrl = "http://localhost:5000"

http {
    POST baseUrl
    body

    jsonSerialize
        {|
         Addressee = "Barry"
         Tone = "Mean"
        |}
}
|> Request.send