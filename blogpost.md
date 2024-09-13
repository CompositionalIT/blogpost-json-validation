
# Deserializing JSON in F#

Ingesting data in a Giraffe app might seem trivial, given it's very convenient JSON `ctx.BindJsonASync<T>() function. But there are a few common pitfalls here. In this blog post, we'll cover how to make sure you deserialize safely, either using these built-in functions with our own validation, or using Thoth.Json to make your deserialization super smart!


Throughout this post, we'll expand on this simple handler, expanding on it in ways that will expose the pitfalls one by one.

```fsharp
type Greeting = { Addressee: string }

let indexHandler next (ctx: HttpContext) =
    task {
        let! greeting = ctx.BindJsonAsync<Greeting>()
        let message = $"Hello world {greeting.Addressee}, from Giraffe!"
        return! text message next ctx
    }
```

So far, we're just using records with strings, and the serializer happily deals with correct requests:

```json
{"addressee":"Barry"}
```

returns

```text
Hello world Barry, from Giraffe!
```

## Pitfall 1: Complex types and decoding directly into your rich domain

As soon as we start expanding our model a bit, things go south. Let's see what happens if we add a Discriminated Union to our Greeting:

```text
type Tone =
    | Formal
    | Casual
 
type Greeting = {
    Addressee: string
    Tone: Tone
}

let greetingHandler next (ctx: HttpContext) =
    task {
        let! greeting = ctx.BindJsonAsync<Greeting>()
        let message =
            match greeting.Tone with
            | Formal -> $"Salutations, {greeting.Addressee}. With the highest respect, Giraffe"
            | Casual -> $"Hello world {greeting.Addressee}, from Giraffe!"
        return! text message next ctx
    }
```
Simply passing the tone as string does not work: 

```json
{"addressee":"Barry","tone":"casual"}
```

returns a 500 response:

```text
No 'Case' property with union name found. Path '', line 1, position 37.
```

### Ugly requests

The JSON deserializer dictates the format `{ case: string; fields: string list}`. It makes for a pretty ugly request body:

```json
{"addressee":"Barry","tone":{"case":"casual"}}
```
returns 
```text
Hello world Barry, from Giraffe!
```

### Tight coupling

What's even worse, about directly serializing into your domain is that your API becomes super tightly coupled with your domain. Add an extra field to a record? The consumer needs to know. Change the name of a union case? Consumer needs to know!

Instead, split the data you ingest into very simple DTO's that can exist out of simple records. You can transform them into domain types easily:

```fsharp
module Domain =
    type Tone =
        | Formal
        | Casual
     
    type Greeting = {
        Addressee: string
        Tone: Tone
    }

module Dto =
    type Greeting = {
        Addressee: string
        Tone: string
    }
    
    let toneFromString =
        function
        | "Formal" -> Ok Domain.Tone.Formal
        | "Casual" -> Ok Domain.Tone.Casual
        | unknown -> Error $"Unknown tone {unknown}"
        
    let greetingFromDto dto =
        result {
            let! tone = toneFromString dto.Tone

            return
                { Domain.Addressee = dto.Addressee
                  Domain.Tone = tone }
        }
        
open Domain
let greetingHandler next (ctx: HttpContext) =
    task {
        let! greeting = ctx.BindJsonAsync<Dto.Greeting>()
        
        match Dto.greetingFromDto greeting with
        | Ok greeting ->
            let message =
                match greeting.Tone with
                | Formal -> $"Salutations, {greeting.Addressee}. With the highest respect, Giraffe"
                | Casual -> $"Hello world {greeting.Addressee}, from Giraffe!"
            return! text message next ctx
        | Error e ->
            return! (setStatusCode 400 >=> text e) next ctx
    }
    }
```

We gained a bit of complexity on the server, but the consumer gained:

* Simple requests
* Nicer error messages, with an appropriate status code

## Pitfall 2: decoding into null!

Everything seems OK, but what if the consumer forgets a field?

```json
{"tone":"Casual"}
```

returns 

```text
Hello world , from Giraffe!
```

Something is clearly missing; so far, we have not used any functions that fail on null, but don't be fooled; `Adressee` was not decoded into an empty string; it was decoded into null!

This is a sneaky one: as you see, the null values spit out by the deserializer can stay hidden pretty well, until at some point they pop up as a runtime error! Let's improve our validation; for brevity, we do ommit creating the domain type that we would generally make in cases like this:




### System.Text.Json decodes into NULL if values are missing! you need to validate everything that can be null including a null check

## Pitfall 3: Giving errors one by one

```
Nice lil example of using validation CU for validation, and sending a 404 message on failure
```

## Decoding made fun: Thoth.JSON

# Thoth makes decoding more complex JSON easier by allowing you to make composable decoders; you can actually go pretty far in domain validation by using Decode.AndThen

```
Nice lil example of using Thoth.Json with "andThen" to deserialize a DU
```


## Conclusion: Giraffe's built-in JSON decoder does the job, if you are aware of it's pitfalls; in case of more complex deserialization, Thoth can be of huge help