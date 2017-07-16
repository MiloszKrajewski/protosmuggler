## Why?

I wanted to play with [Proto.Actor](https://github.com/AsynkronIT/protoactor-dotnet) a little bit. By default it uses Protocol Buffers for serialization.  Protocol.Buffers are powerful and fast, they are not experimentation friendly, though. 

Problems:
* I already had messages annotated with `Newtonsoft.Json` attributes
* I wanted to pass messages I got on REST API straight to actors

Of course, Smuggler is much slower than Protocol Buffer native messages, but allows to prototype fast.

## Plug-in serializer implementation

```csharp
foreach (var d in Smuggler.Descriptors) 
    Serialization.RegisterFileDescriptor(d);

Smuggler.Use(
    message => ToByteArray(message),
    bytes => FromByteArray(bytes)
)
```