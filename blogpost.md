
# Deserializing JSON in F#

Ingesting data in a Giraffe app might seem trivial, given it's very convenient JSON `ctx.BindJsonASync<T>() function. But there are a few common pitfalls here. In this blog post, we'll cover how to make sure you deserialize safely, either using these built-in functions with our own validation, or using Thoth.Json to make your deserialization super smart!

## Pitfall 1: Complex types and decoding directly into your rich domain

### System.Text.Json does not decode into DUs; there is other tooling that does do it, so you can overcome it, but should you?

### NO you should not! keep your domain and DTOs separate, so your model and API can develop independently


## Pitfall 2: decoding into null!

### System.Text.Json decodes into NULL if values are missing! you need to validate everything that can be null including a null check

```
Nice lil example of using validation CU for validation, and sending a 404 message on failure
```

## Decoding made fun: Thoth.JSON

# Thoth makes decoding more complex JSON easier by allowing you to make composable decoders; you can actually go pretty far in domain validation by using Decode.AndThen

```
Nice lil example of using Thoth.Json with "andThen" to deserialize a DU
```


## Conclusion: Giraffe's built-in JSON decoder does the job, if you are aware of it's pitfalls; in case of more complex deserialization, Thoth can be of huge help