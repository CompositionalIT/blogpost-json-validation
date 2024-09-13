

Ingesting data in a Giraffe app might seem trivial, given it's very convenient JSON `ctx.BindJsonASync<T>()` function. But there are a few common pitfalls here. In this blog post, we'll cover how to make sure you deserialize safely!


Throughout this post, we'll expand on this simple handler, expanding on it in ways that will expose the pitfalls one by one.

```fsharp
type Greeting = { Addressee: string }

let indexHandler next (ctx: HttpContext) = task {
        let! greeting = ctx.BindJsonAsync<Greeting>()
        let message = sprintf "Hello world %s, from Giraffe!" greeting.Addressee
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

# Pitfall 1: Complex types and decoding directly into your rich domain

As soon as we start expanding our model a bit, things go south. Let's see what happens if we add a Discriminated Union to our Greeting:

```text
type Tone =
    | Formal
    | Casual
 
type Greeting = {
    Addressee: string
    Tone: Tone
}

let greetingHandler next (ctx: HttpContext) = task {
        let! greeting = ctx.BindJsonAsync<Greeting>()
        let message =
            match greeting.Tone with
            | Formal -> sprintf "Salutations, %s. With the highest respect, Giraffe" greeting.Addressee
            | Casual -> sprintf "Hello world %s, from Giraffe!" greeting.Addressee

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

## Ugly requests

The JSON deserializer dictates the format `{ case: string; fields: string list}`. It makes for a pretty ugly request body:

```json
{"addressee":"Barry","tone":{"case":"casual"}}
```
returns 
```text
Hello world Barry, from Giraffe!
```

## Tight coupling

What's even worse, about directly serializing into your domain is that your API becomes super tightly coupled with your domain. Add an extra field to a record? The consumer needs to know. Change the name of a union case? Consumer needs to know!

Instead, split out the data you ingest into very simple DTO's that can exist out of simple records. You can transform them into domain types easily. We use [FsToolkit.ErrorHandling](https://demystifyfp.gitbook.io/fstoolkit-errorhandling/fstoolkit.errorhandling
)'s result Computation Expression to validate records.

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
        
    let greetingFromDto dto = result {
            let! tone = toneFromString dto.Tone

            return
                { Domain.Addressee = dto.Addressee
                  Domain.Tone = tone }
        }
        
open Domain

let greetingHandler next (ctx: HttpContext) = task {
        let! greeting = ctx.BindJsonAsync<Dto.Greeting>()

        match Dto.greetingFromDto greeting with
        | Ok greeting ->
            let message =
                match greeting.Tone with
                | Formal -> sprintf "Salutations, %s. With the highest respect, Giraffe" greeting.Addressee
                | Casual -> sprintf "Hello world %s, from Giraffe!" greeting.Addressee
            return! text message next ctx
        | Error e ->
            return! (setStatusCode 400 >=> text e) next ctx
    }
    }
```

We can now make the request again, in a very simple format:

```json
{"addressee":"Barry","tone":"Casual"}
```

```text
Hello world Barry, from Giraffe!
```

And if we use a wrong value for tone, we get a nice error message:

```json
{"addressee":"Barry","tone":"Mean"}
```

```text
Unknown tone Mean
```


We gained a bit of complexity on the server, but the consumer gained:

* Simple requests
* Nicer error messages, with an appropriate status code
* Less frequent API changes, because the API model is separate from the domain

# Pitfall 2: decoding into null!

Everything seems OK, but what if the consumer forgets a field?

```json
{"tone":"Casual"}
```

returns 

```text
Hello world , from Giraffe!
```

Something is clearly missing; so far, we have not used any functions that fail on null, but don't be fooled; `Adressee` was not decoded into an empty string; it was decoded into null!

This is a sneaky one: as you see, the null values spit out by the deserializer can stay hidden pretty well, until at some point they pop up as a runtime error! Let's improve our validation; for brevity, we do omit creating the domain type that we would generally make in cases like this:

```fsharp
    let validateAddressee addressee =
        if addressee |> String.IsNullOrWhiteSpace then
            Error "Missing addressee"
        else
            Ok addressee

    let greetingFromDto dto =
        result {
            let! tone = toneFromString dto.Tone
            let! addressee = validateAddressee dto.Addressee

            return
                { Domain.Addressee = addressee
                  Domain.Tone = tone }
        }
```

That's a lot better!

```json
{"tone":"Casual"}
```

returns a 400 response:

```text
Missing addressee
```

# Pitfall 3: Giving errors one by one

We're validating our `Tone` and `Addressee`, but what if both are missing?

```json
{}
```

returns a 400 response:

```text
Unknown tone 
```

No lies there, but definitely not the whole truth either; It's pretty annoying to deal with one error, for another one to pop up:

```json
{"tone":"Casual"}
```

returns another 400 response:

```text
Missing addressee
```

Ideally, you'd see all errors at the same time. Fortunately, FsToolkit.ErrorHandling has the great [`validation` Computation Expression](https://www.compositional-it.com/news-blog/validation-with-f-5-and-fstoolkit/), that, when used with the `and!` keyword, instead of immediately returning errors when they are encountered, adds them to a list:

```text
module Dto =
    ...
    let greetingFromDto dto = validation {
        let! tone = toneFromString dto.Tone
        and! addressee = validateAddressee dto.Addressee

        return
            { Domain.Addressee = addressee
              Domain.Tone = tone }
    }
...
let greetingHandler next (ctx: HttpContext) = task {
        let! greeting = ctx.BindJsonAsync<Dto.Greeting>()

        match Dto.greetingFromDto greeting with
        | Ok greeting ->
            ...
        | Error e ->
            let errorMessage = String.concat "; " e
            return! (setStatusCode 400 >=> text errorMessage ) next ctx
    }

```

If we make the empty request again, we get a list of errors:

```text
Unknown tone ; Missing addressee
```

# Conclusion

As you can see, Giraffe's `ctx.BindJsonAsync<Greeting>()` provides a great starting point for decoding JSON, but to create a usable API it does require a few good practices when it comes to parsing data. It's not very suitable for decoding straight into the domain, although you probably don't want to do that anyway, if you want your API to be maintainable. Always be wary of the possibility of null values sneaking into your app through the serializer, and make sure you parallelize your validation so your consumers don't waste time dealing with validation errors one by one!

If you're dealing with more complex JSON, we highly recommend checking out `Thoth.Json`. It deals with most issues we addressed here out of the box. It has a bit of a learning curve, but once you get the hang of it, it provides a great way to write composable JSON decoders, with parallel validation included out of the box! We'll dedicate a blog post to it in the near future.