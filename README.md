## Why?

I wanted to play with [Proto.Actor](https://github.com/AsynkronIT/protoactor-dotnet) a little bit. By default it uses Protocol Buffers for serialization.  Protocol.Buffers are powerful and fast, they are not experimentation friendly, though. 

Problems:
* I already had messages annotated with `Newtonsoft.Json` attributes
* I wanted to pass messages I got on REST API straight to actors

Of course, Smuggler is much slower than Protocol Buffer native messages, but allows to prototype fast.

## Plug-in serializer implementation

```csharp
// register protobuf descriptor
Smuggler.Descriptors.ForEach(Serialization.RegisterFileDescriptor);

// setup serializer (your custom implementation)
Smuggler.Use(
    message => ToByteArray(message),
    bytes => FromByteArray(bytes)
)
```

## Newtonsoft adapter included

Proto.Smuggler includes Nwwtonsoft adapter:

```
Smuggler.Use(new JsonSmuggler(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));
```

so you can can easily smuggle all messages which are serializable with Newtonsoft.Jsom.

## Smuggling

Smuggler is very simplistic and rather for prototyping so you need to explicilty use in you actors. When you was to serialize message use: `Smuggler.Conceal(message)`. To deserialize use `Smuggler.Reveal(message)`.
