namespace Xifias

module Request =

    open System
    open System.Net
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Primitives


    /// A helper to convert StringValues into a string option.
    let stringValuesString (sv: StringValues) =
        if sv.Count <> 0 then
            Some (sv.ToString())
        else
            None


    /// Convert an option to a boolean value
    let toBool o =
        match o with
        | Some _ ->
            true
        | None ->
            false


    /// <summary>Get the remote IP of the request.</summary>
    /// <remarks>
    ///     NOTE: This IP address is the remote IP from the TCP connection.
    ///     When following best practices -- putting Kestrel behind a reverse proxy server like
    ///     nginx, IIS, or a cloud API gateway provider -- this will be the reverse proxy IP, not the client IP.
    ///</remarks>
    let remoteIp (context: HttpContext) =
        context.Connection.RemoteIpAddress


    /// <summary>Get the remote port of the request.</summary>
    /// <remarks>
    ///     NOTE: This port is the remote port from the TCP connection.
    ///     When following best practices -- putting Kestrel behind a reverse proxy server like
    ///     nginx, IIS, or a cloud API gateway provider -- this will be the reverse proxy port, not the client port.
    ///</remarks>
    let remotePort (context: HttpContext) =
        context.Connection.RemotePort


    /// Get the local IP of the request.
    let localIp (context: HttpContext) =
        context.Connection.LocalIpAddress


    /// Get local port of the request.
    let localPort (context: HttpContext) =
        context.Connection.LocalPort


    /// Get the client certificate of the request.
    let clientCertificate (context: HttpContext) =
        context.Connection.ClientCertificate


    /// <summary>Get the host name provided by the Host header.</summary>
    /// <remarks>
    ///     NOTE: This value is may be provided by the client, rendering it insecure. Use with caution.
    /// </remarks>
    let hostName (context: HttpContext) =
        context.Request.Host.Host


    /// <summary>Get the host port provided by the Host header.</summary>
    /// <remarks>
    ///     NOTE: This value is may be provided by the client, rendering it insecure. Use with caution.
    /// </remarks>
    let hostPort (context: HttpContext) =
        Option.ofNullable context.Request.Host.Port


    /// Get the first header which matches the provided filter function
    let headerFilter f (context: HttpContext) =
        let rec loop names =
            match names with
            | [] ->
                None
            | name :: rest ->
                match f name context.Request.Headers.[name] with
                | None ->
                    loop rest
                | x ->
                    x

        loop (List.ofSeq context.Request.Headers.Keys)


    /// Get whether the header exists with the provided name
    let hasHeaderName name (context: HttpContext) =
        context.Request.Headers.ContainsKey name


    /// Get the value for the provided header name
    let headerValue name (context: HttpContext) =
        match context.Request.Headers.TryGetValue name with
        | false, _ ->
            None
        | true, v ->
            Some v


    /// Get the value for the provided header name and matching the filter function
    let headerValueFilter name valuef (context: HttpContext) =
        match context.Request.Headers.TryGetValue name with
        | false, _ ->
            None
        | true, v ->
            valuef v


    /// A wrapper for IPAddress.TryParse that converts to an option
    let parseIp (s: string) =
        match IPAddress.TryParse s with
        | false, _ ->
            None
        | true, ip ->
            Some ip


    /// <summary>Get the client IP from the given header.</summary>
    /// <remarks>
    ///     NOTE: Headers are insecure and unreliable since they can be set from clients.
    ///     This function is provided so you can check for the specific headers that you know are set appropiately by your hosting environment.
    ///     Here is an example:
    ///     <code>
    ///     let clientIp : IPAddress option =
    ///         clientIpFromHeader "X-Forwarded-For" httpContext
    ///         |> Option.orElse (clientIpFromHeader "X-Real-IP" httpContext)
    ///     </code>
    /// </remarks>
    let clientIpFromHeader name (context: HttpContext) =

        let skip f x =
            if f x then
                None
            else
                Some x

        let isLocal (ip: IPAddress) =
            let bytes = ip.GetAddressBytes()
            bytes.[0] = byte 10
            || bytes.[0] = byte 127
            || ( bytes.[0] = byte 172 && bytes.[1] >= byte 16 && bytes.[1] < byte 32 )
            || ( bytes.[0] = byte 192 && bytes.[1] = byte 168 )

        let (>>=) x f = Option.bind f x
        let (<!>) x f = Option.map f x

        let tryGetIp s () =
            skip String.IsNullOrWhiteSpace s
            <!> fun s -> s.Trim()
            >>= skip ((=) "::1") // ignore ipv6 link local
            >>= parseIp
            >>= skip isLocal

        let ipFolder found (s: string) =
            found
            |> Option.orElseWith (tryGetIp s)

        let firstPublicIp (sv: StringValues) =
            stringValuesString sv
            |> Option.bind ( fun s ->
                s.Split(',')
                |> Array.fold ipFolder None
            )

        headerValue name context
        |> Option.bind firstPublicIp


    /// Get the content type of the request body
    let contentType (context: HttpContext) =
        // can be null
        // ref https://github.com/aspnet/HttpAbstractions/blob/release/2.1/src/Microsoft.AspNetCore.Http/Internal/DefaultHttpResponse.cs#L80
        if String.IsNullOrWhiteSpace context.Request.ContentType then
            None
        else
            Some context.Request.ContentType


    /// Get the cookie with
    let cookieFilter f (context: HttpContext) =
        let rec loop names =
            match names with
            | [] ->
                None
            | name :: rest ->
                match f name context.Request.Cookies.[name] with
                | None ->
                    loop rest
                | x ->
                    x

        loop (List.ofSeq context.Request.Cookies.Keys)


    /// Get whether the cookie exists with the provided name
    let hasCookieName name (context: HttpContext) =
        context.Request.Cookies.ContainsKey name


    /// Get the cookie with the provided name
    let cookieValue (name: string) (context: HttpContext) =
        match context.Request.Cookies.TryGetValue name with
        | false, _ ->
            None
        | true, v ->
            Some v


    /// Get the value for the provided cookie name and matching the filter function
    let cookieValueFilter (name: string) valuef (context: HttpContext) =
        match context.Request.Cookies.TryGetValue name with
        | false, _ ->
            None
        | true, v ->
            valuef v


    /// Get whether this request is over HTTPS.
    let isHttp (context: HttpContext) =
        context.Request.IsHttps


    /// Get the scheme of the request.  For example: in http://my.foo:123/bar, "http" is the scheme
    let scheme (context: HttpContext) =
        context.Request.Scheme


    /// Get the protocol of the request. For example: HTTP/1.1
    let protocol (context: HttpContext) =
        context.Request.Protocol


    /// Get the HTTP method of the request. Examples: GET, POST
    let verb (context: HttpContext) =
        context.Request.Method


    /// Get the path of the request. Example: /api/something
    let path (context: HttpContext) =
        context.Request.Path


    /// Get the user of the request.
    let user (context: HttpContext) =
        Option.ofObj context.User


    /// Get the claims of the user present on the request.
    let claims (context: HttpContext) =
        Option.ofObj context.User
        |> Option.bind (fun user -> user.Claims |> Option.ofObj)
        |> Option.defaultValue Seq.empty


    /// Parse the claim with the provided name from the user present on the request.
    /// If multiple claims of the same name are present, the first one will be returned.
    let claimParse fOption (name: string) (context: HttpContext) =
        context.User
        |> Option.ofObj
        |> Option.bind (fun u -> u.FindFirst(name) |> Option.ofObj)
        |> Option.bind (fun claim -> fOption claim.Value)


    /// Get the claim with the provided name from the user present on the request.
    /// If multiple claims of the same name are present, the first one will be returned.
    let claim (name: string) (context: HttpContext) =
        claimParse Some name context


    /// Get claims of the provided type from the user present on the request. Multiple of the same claim may be present.
    let claimsOfType (name: string) (context: HttpContext) =
        claims context
            |> Seq.filter (fun x -> x.Type = name)


    /// Get the first claim of the provided type from the user present on the request.
    let claimOfType (name: string) (context: HttpContext) =
        claimsOfType name context
            |> Seq.tryHead


    /// If the user present on the request has a claim of the provided type, returns true. Otherwise returns false.
    let hasClaimOfType (name: string) (context: HttpContext)= 
        claimsOfType name context
            |> (not << Seq.isEmpty)


    /// If the user present on the request has the exact claim provided, returns true. Otherwise returns false.
    let hasClaim (name: string) (value: string) (context: HttpContext) =
        claims context
            |> Seq.exists (fun x -> x.Type = name && x.Value = value)


    /// Get the origin for the request.
    let origin (context: HttpContext) =
        headerValue "Origin" context
            |> Option.bind stringValuesString

