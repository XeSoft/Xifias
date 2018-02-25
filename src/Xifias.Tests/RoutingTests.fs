namespace Xifias.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open Xifias
open Microsoft.AspNetCore.Http
open System.Net
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Http.Features
open Microsoft.AspNetCore.Http.Internal
open System.Collections.Generic

[<TestClass>]
type Routing() =

    let failRoute _ = None

    let passRoute1 _ = Some 1

    let passRoute2 _ = Some 2

    let getContext () = { HttpContext = new DefaultHttpContext(); Handler = None }

    let getIp s = IPAddress.Parse(s)

    let userWithClaims isAuthenticated (claims : (string * string) list) =
        let claims = claims |> List.map (fun (n, v) -> new System.Security.Claims.Claim(n, v))
        new System.Security.Claims.ClaimsPrincipal(
            if isAuthenticated then
                new System.Security.Claims.ClaimsIdentity(claims, "custom")
            else
                new System.Security.Claims.ClaimsIdentity(claims)
        )

    let printFail (expected : 'a) (actual : 'a) =
        sprintf "\r\n\r\nEXPECTED\r\n%A\r\n\r\nACTUAL\r\n%A\r\n" expected actual

    let areEqual (expected: 'a) (actual: 'a) =
         Assert.IsTrue((expected = actual), printFail expected actual)

    let isNone (a : 'a option) =
        match a with
            | None ->
                ()

            | Some _ ->
                Assert.Fail(printFail None a)


    let isSome (a : 'a option) =
        match a with
            | Some _ ->
                ()

            | None ->
                Assert.Fail(sprintf "\r\n\r\nEXPECTED\r\nSome ...\r\n\r\nACTUAL\r\n%A\r\n" a)

    (*
        andThen
    *)

    [<TestMethod>]
    member __.``andThen returns None when first RouteHandler returns None`` () =
        let actual = None |> andThen passRoute1
        areEqual None actual

    [<TestMethod>]
    member __.``andThen returns None when second RouteHandler returns None`` () =
        let actual = Some 0 |> andThen failRoute
        areEqual None actual

    [<TestMethod>]
    member __.``andThen returns None when both RouteHandlers return None`` () =
        let actual = None |> andThen failRoute
        areEqual None actual

    [<TestMethod>]
    member __.``andThen returns Some when both RouteHandlers return Some`` () =
        let actual = Some 0 |> andThen passRoute2
        areEqual (Some 2) actual


    (*
        composeWith
    *)

    [<TestMethod>]
    member __.``composeWith returns None when first RouteHandler returns None`` () =
        let actual = (failRoute |> composeWith passRoute1) 0
        areEqual None actual

    [<TestMethod>]
    member __.``composeWith returns None when second RouteHandler returns None`` () =
        let actual = (passRoute1 |> composeWith failRoute) 0
        areEqual None actual

    [<TestMethod>]
    member __.``composeWith returns None when both RouteHandlers return None`` () =
        let actual = (failRoute |> composeWith failRoute) 0
        areEqual None actual

    [<TestMethod>]
    member __.``composeWith returns Some when both RouteHandlers return Some`` () =
        let actual = (passRoute1 |> composeWith passRoute2) 0
        areEqual (Some 2) actual


    (*
        orElse
    *)

    [<TestMethod>]
    member __.``orElse returns Some when first RouteHandler returns None, second returns Some`` () =
        let actual = (failRoute |> orElse passRoute1) 0
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``orElse returns Some when first RouteHandler returns Some, second returns None`` () =
        let actual = (passRoute1 |> orElse failRoute) 0
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``orElse returns None when both RouteHandlers return None`` () =
        let actual = (failRoute |> orElse failRoute) 0
        areEqual None actual

    [<TestMethod>]
    member __.``orElse returns first Some when both RouteHandlers return Some`` () =
        let actual = (passRoute2 |> orElse passRoute1) 0
        areEqual (Some 2) actual


    (*
        >>=
    *)

    [<TestMethod>]
    member __.``>>= returns None when first RouteHandler returns None`` () =
        let actual = None >>= passRoute1
        areEqual None actual

    [<TestMethod>]
    member __.``>>= returns None when second RouteHandler returns None`` () =
        let actual = Some 0 >>= failRoute
        areEqual None actual

    [<TestMethod>]
    member __.``>>= returns None when both RouteHandlers return None`` () =
        let actual = None >>= failRoute
        areEqual None actual

    [<TestMethod>]
    member __.``>>= returns Some when both RouteHandlers return Some`` () =
        let actual = Some 0 >>= passRoute2
        areEqual (Some 2) actual


    (*
        >=>
    *)

    [<TestMethod>]
    member __.``>=> returns None when first RouteHandler returns None`` () =
        let actual = (failRoute >=> passRoute1) 0
        areEqual None actual

    [<TestMethod>]
    member __.``>=> returns None when second RouteHandler returns None`` () =
        let actual = (passRoute1 >=> failRoute) 0
        areEqual None actual

    [<TestMethod>]
    member __.``>=> returns None when both RouteHandlers return None`` () =
        let actual = (failRoute >=> failRoute) 0
        areEqual None actual

    [<TestMethod>]
    member __.``>=> returns Some when both RouteHandlers return Some`` () =
        let actual = (passRoute1 >=> passRoute2) 0
        areEqual (Some 2) actual


    (*
        <|>
    *)

    [<TestMethod>]
    member __.``<|> returns Some when first RouteHandler returns None, second returns Some`` () =
        let actual = (failRoute <|> passRoute1) 0
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``<|> returns Some when first RouteHandler returns Some, second returns None`` () =
        let actual = (passRoute1 <|> failRoute) 0
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``<|> returns None when both RouteHandlers return None`` () =
        let actual = (failRoute <|> failRoute) 0
        areEqual None actual

    [<TestMethod>]
    member __.``<|> returns first Some when both RouteHandlers return Some`` () =
        let actual = (passRoute2 <|> passRoute1) 0
        areEqual (Some 2) actual


    (*
        oneOf
    *)

    [<TestMethod>]
    member __.``oneOf returns first route handler when both return Some`` () =
        let actual = oneOf [ passRoute1; passRoute2 ] 0
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``oneOf returns second route handler when first returns None`` () =
        let actual = oneOf [ failRoute; passRoute2 ] 0
        areEqual (Some 2) actual

    [<TestMethod>]
    member __.``oneOf returns None when both handers return None`` () =
        let actual = oneOf [ failRoute; failRoute ] 0
        areEqual None actual


    (*
        filter
    *)

    [<TestMethod>]
    member __.``filter returns Some if function returns true`` () =
        let actual = filter (fun _ -> true) (getContext ())
        isSome actual

    [<TestMethod>]
    member __.``filter returns None if function returns false`` () =
        let actual = filter (fun _ -> false) (getContext ())
        isNone actual


    (*
        filterValue
    *)

    [<TestMethod>]
    member __.``filterValue returns Some if function returns same as passed in value`` () =
        let actual = filterValue (fun ctx -> ctx.Request.Path.ToString()) "" (getContext ())
        isSome actual

    [<TestMethod>]
    member __.``filterValue returns None if function returns different from passed in value`` () =
        let actual = filterValue (fun ctx -> ctx.Request.Path.ToString()) "not this" (getContext ())
        isNone actual


    (*
        remoteIp
    *)

    [<TestMethod>]
    member __.``remoteIp returns Some if IP does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.RemoteIpAddress <- getIp "127.0.0.5"
        let actual = remoteIp (getIp "127.0.0.5") ctx
        isSome actual

    [<TestMethod>]
    member __.``remoteIp returns None if IP does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.RemoteIpAddress <- getIp "127.0.0.5"
        let actual = remoteIp (getIp "127.0.0.4") ctx
        isNone actual


    (*
        remotePort
    *)

    [<TestMethod>]
    member __.``remotePort returns Some if port does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.RemotePort <- 255
        let actual = remotePort 255 ctx
        isSome actual

    [<TestMethod>]
    member __.``remotePort returns None if port does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.RemotePort <- 255
        let actual = remotePort 1 ctx
        isNone actual


    (*
        localIp
    *)

    [<TestMethod>]
    member __.``localIp returns Some if IP does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.LocalIpAddress <- getIp "127.0.0.2"
        let actual = localIp (getIp "127.0.0.2") ctx
        isSome actual

    [<TestMethod>]
    member __.``localIp returns None if IP does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.LocalIpAddress <- getIp "127.0.0.2"
        let actual = localIp (getIp "127.0.0.1") ctx
        isNone actual


    (*
        localPort
    *)

    [<TestMethod>]
    member __.``localPort returns Some if IP does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.LocalPort <- 512
        let actual = localPort 512 ctx
        isSome actual

    [<TestMethod>]
    member __.``localPort returns None if IP does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Connection.LocalPort <- 512
        let actual = localPort 2 ctx
        isNone actual


    (*
        clientCertificateFilter
    *)

    [<TestMethod>]
    member __.``clientCertificateFilter returns Some if filter returns true`` () =
        let ctx = getContext ()
        let actual = clientCertificateFilter (fun _ -> true) ctx
        isSome actual

    [<TestMethod>]
    member __.``clientCertificateFilter returns None if filter returns false`` () =
        let ctx = getContext ()
        let actual = clientCertificateFilter (fun _ -> false) ctx
        isNone actual


    (*
        hostName
    *)

    [<TestMethod>]
    member __.``hostName returns Some if name does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Host <- HostString("foo.com")
        let actual = hostName "foo.com" ctx
        isSome actual

    [<TestMethod>]
    member __.``hostName returns None if name does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Host <- HostString("foo.com")
        let actual = hostName "bar.com" ctx
        isNone actual


    (*
        hostPort
    *)

    [<TestMethod>]
    member __.``hostPort returns Some if port does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Host <- HostString("foo.com:999")
        let actual = hostPort (Some 999) ctx
        isSome actual

    [<TestMethod>]
    member __.``hostPort returns None if port does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Host <- HostString("foo.com:999")
        let actual = hostPort (Some 998) ctx
        isNone actual


    (*
        headerFilter
    *)

    [<TestMethod>]
    member __.``headerFilter returns Some if filter returns true`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerFilter (fun _ _ -> true) ctx
        isSome actual

    [<TestMethod>]
    member __.``headerFilter returns None if filter returns false`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerFilter (fun _ _ -> false) ctx
        isNone actual

    [<TestMethod>]
    member __.``headerFilter returns None if there are no headers`` () =
        let ctx = getContext ()
        let actual = headerFilter (fun _ _ -> true) ctx
        isNone actual


    (*
        headerValueFilter
    *)

    [<TestMethod>]
    member __.``headerValueFilter returns Some if a header with the given name is found and the value filter function returns true`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerValueFilter "X-99" (fun _ -> true) ctx
        isSome actual

    [<TestMethod>]
    member __.``headerValueFilter returns None if a header with the given name is found and the value filter function returns false`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerValueFilter "X-99" (fun _ -> false) ctx
        isNone actual

    [<TestMethod>]
    member __.``headerValueFilter returns None if a header with the given name is not found`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerValueFilter "X-98" (fun _ -> true) ctx
        isNone actual

    [<TestMethod>]
    member __.``headerValueFilter returns None if there are no headers`` () =
        let ctx = getContext ()
        let actual = headerValueFilter "" (fun _ -> true) ctx
        isNone actual


    (*
        headerName
    *)

    [<TestMethod>]
    member __.``headerName returns Some if a header with the given name is found`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerName "X-99" ctx
        isSome actual

    [<TestMethod>]
    member __.``headerName returns None if a header with the given name is not found`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerName "X-98" ctx
        isNone actual


    (*
        headerValue
    *)

    [<TestMethod>]
    member __.``headerValue returns Some if a header with the given name is found and the value does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerValue "X-99" "Trepidation" ctx
        isSome actual

    [<TestMethod>]
    member __.``headerValue returns None if a header with the given name is found and the value does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerValue "X-99" "Elation" ctx
        isNone actual

    [<TestMethod>]
    member __.``headerValue returns None if a header with the given name is not found`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-99", StringValues("Trepidation"))
        let actual = headerValue "X-98" "Trepidation" ctx
        isNone actual


    (*
        clientIpFromHeader
    *)

    [<TestMethod>]
    member __.``clientIpFromHeader returns Some if name is found and value does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-Real-IP", StringValues("1.2.3.4"))
        let actual = clientIpFromHeader "X-Real-IP" "1.2.3.4" ctx
        isSome actual

    [<TestMethod>]
    member __.``clientIpFromHeader returns Some if name is found and first public IP matches value`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-Real-IP", StringValues("127.0.0.1, 10.0.0.1, 192.168.0.1, 1.2.3.4, 2.3.4.5, 172.16.0.1"))
        let actual = clientIpFromHeader "X-Real-IP" "1.2.3.4" ctx
        isSome actual

    [<TestMethod>]
    member __.``clientIpFromHeader returns None if name is found and no public IP is found`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-Real-IP", StringValues("127.0.0.1, 10.0.0.1, 192.168.0.1, 172.16.0.1"))
        let actual = clientIpFromHeader "X-Real-IP" "127.0.0.1" ctx
        isNone actual

    [<TestMethod>]
    member __.``clientIpFromHeader returns None if name is found and value does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.Headers.Add("X-Real-IP", StringValues("1.2.3.4"))
        let actual = clientIpFromHeader "X-Real-IP" "2.3.4.5" ctx
        isNone actual

    [<TestMethod>]
    member __.``clientIpFromHeader returns None if name is not found`` () =
        let ctx = getContext ()
        let actual = clientIpFromHeader "X-Real-IP" "1.2.3.4" ctx
        isNone actual


    (*
        contentType
    *)

    [<TestMethod>]
    member __.``contentType returns Some if content type does match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.ContentType <- "application/json"
        let actual = contentType "application/json" ctx
        isSome actual

    [<TestMethod>]
    member __.``contentType returns None if content type does not match`` () =
        let ctx = getContext ()
        ctx.HttpContext.Request.ContentType <- "application/json"
        let actual = contentType "text/plain" ctx
        isNone actual


    (*
        cookieFilter
    *)

    [<TestMethod>]
    member __.``cookieFilter returns Some if the filter returns true`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieFilter (fun _ _ -> true) ctx
        isSome actual

    [<TestMethod>]
    member __.``cookieFilter returns None if the filter returns false`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieFilter (fun _ _ -> false) ctx
        isNone actual

    [<TestMethod>]
    member __.``cookieFilter returns None if there are no headers`` () =
        let ctx = getContext ()
        let actual = cookieFilter (fun _ _ -> true) ctx
        isNone actual


    (*
        cookieValueFilter
    *)

    [<TestMethod>]
    member __.``cookieValueFilter returns Some if the name matches the provided value and the filter returns true`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieValueFilter "WhiteChocolateMacadamia" (fun _ -> true) ctx
        isSome actual

    [<TestMethod>]
    member __.``cookieValueFilter returns None if the filter returns false`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieValueFilter "WhiteChocolateMacadamia" (fun _ -> false) ctx
        isNone actual

    [<TestMethod>]
    member __.``cookieValueFilter returns None if there are no headers`` () =
        let ctx = getContext ()
        let actual = cookieValueFilter "WhiteChocolateMacadamia" (fun _ -> true) ctx
        isNone actual


    (*
        cookieName
    *)

    [<TestMethod>]
    member __.``cookieName returns Some if a cookie with the provided name is present`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieName "WhiteChocolateMacadamia" ctx
        isSome actual

    [<TestMethod>]
    member __.``cookieName returns None if a cookie with the provided name is not present`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieName "ChocolateChip" ctx
        isNone actual

    [<TestMethod>]
    member __.``cookieName returns None if there are no cookies`` () =
        let ctx = getContext ()
        let actual = cookieName "WhiteChocolateMacadamia" ctx
        isNone actual


    (*
        cookieValue
    *)

    [<TestMethod>]
    member __.``cookieValue returns Some if a cookie with the provided name and value is present`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieValue "WhiteChocolateMacadamia" "Mmmmmm" ctx
        isSome actual

    [<TestMethod>]
    member __.``cookieValue returns None if a cookie with the provided name is present, but the value does not match`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieValue "WhiteChocolateMacadamia" "" ctx
        isNone actual

    [<TestMethod>]
    member __.``cookieValue returns None if a cookie with the provided name is not present`` () =
        let ctx = getContext ()
        let cookies = Dictionary<string, string>(dict [("WhiteChocolateMacadamia", "Mmmmmm")]) |> RequestCookieCollection
        ctx.HttpContext.Features.Set<IRequestCookiesFeature>(RequestCookiesFeature (cookies))
        let actual = cookieValue "ChocolateChip" "Mmmmmm" ctx
        isNone actual

    [<TestMethod>]
    member __.``cookieValue returns None if there are no cookies`` () =
        let ctx = getContext ()
        let actual = cookieValue "WhiteChocolateMacadamia" "Mmmmmm" ctx
        isNone actual


    (*
        isHttps
    *)

    [<TestMethod>]
    member __.``isHttp returns Some if the request scheme is https`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Scheme <- "https"
        let actual = isHttps ctx
        isSome actual

    [<TestMethod>]
    member __.``isHttp returns None if the request scheme is http`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Scheme <- "http"
        let actual = isHttps ctx
        isNone actual


    (*
        scheme
    *)

    [<TestMethod>]
    member __.``scheme returns Some if the request scheme matches the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Scheme <- "https"
        let actual = scheme "https" ctx
        isSome actual

    [<TestMethod>]
    member __.``scheme returns None if the request scheme does not match the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Scheme <- "https"
        let actual = scheme "http" ctx
        isNone actual


    (*
        protocol
    *)

    [<TestMethod>]
    member __.``protocol returns Some if the request protocol matches the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "HTTP/1.1"
        let actual = protocol "HTTP/1.1" ctx
        isSome actual

    [<TestMethod>]
    member __.``protocol returns None if the request protocol does not match the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "XETP/0.1"
        let actual = protocol "HTTP/1.1" ctx
        isNone actual


    (*
        httpVerb
    *)

    [<TestMethod>]
    member __.``httpVerb returns Some if the request method matches the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "PLAY"
        let actual = httpVerb "PLAY" ctx
        isSome actual

    [<TestMethod>]
    member __.``httpVerb returns None if the request method does not match the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "PLAY"
        let actual = httpVerb "SLEEP" ctx
        isNone actual


    (*
        GET
    *)

    [<TestMethod>]
    member __.``GET returns Some if the request method is GET`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "GET"
        let actual = GET ctx
        isSome actual

    [<TestMethod>]
    member __.``GET returns None if the request method is not GET`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "SET"
        let actual = GET ctx
        isNone actual


    (*
        POST
    *)

    [<TestMethod>]
    member __.``POST returns Some if the request method is POST`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "POST"
        let actual = POST ctx
        isSome actual

    [<TestMethod>]
    member __.``POST returns None if the request method is not POST`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "PRE"
        let actual = POST ctx
        isNone actual


    (*
        PUT
    *)

    [<TestMethod>]
    member __.``PUT returns Some if the request method is PUT`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "PUT"
        let actual = PUT ctx
        isSome actual

    [<TestMethod>]
    member __.``PUT returns None if the request method is not PUT`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "TAKE"
        let actual = PUT ctx
        isNone actual


    (*
        PATCH
    *)

    [<TestMethod>]
    member __.``PATCH returns Some if the request method is PATCH`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "PATCH"
        let actual = PATCH ctx
        isSome actual

    [<TestMethod>]
    member __.``PATCH returns None if the request method is not PATCH`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "RIP"
        let actual = PATCH ctx
        isNone actual


    (*
        DELETE
    *)

    [<TestMethod>]
    member __.``DELETE returns Some if the request method is DELETE`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "DELETE"
        let actual = DELETE ctx
        isSome actual

    [<TestMethod>]
    member __.``DELETE returns None if the request method is not DELETE`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "RESTORE"
        let actual = DELETE ctx
        isNone actual


    (*
        OPTIONS
    *)

    [<TestMethod>]
    member __.``OPTIONS returns Some if the request method is OPTIONS`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Method <- "OPTIONS"
        let actual = OPTIONS ctx
        isSome actual

    [<TestMethod>]
    member __.``OPTIONS returns None if the request method is not OPTIONS`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Protocol <- "IMPERATIVE"
        let actual = OPTIONS ctx
        isNone actual


    (*
        path
    *)

    [<TestMethod>]
    member __.``path returns Some if the request uri and provided value do match`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Path <- "/some/path"
        let actual = path "/some/path" ctx
        isSome actual

    [<TestMethod>]
    member __.``path returns None if the request uri and provided value do not match`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Path <- "/some/path"
        let actual = path "/some/other/path" ctx
        isNone actual


    (*
        pathPrefix
    *)

    [<TestMethod>]
    member __.``pathPrefix returns Some if the request uri starts with the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Path <- "/startsWith/path"
        let actual = pathPrefix "/startsWith" ctx
        isSome actual

    [<TestMethod>]
    member __.``pathPrefix returns None if the request uri does not start with the provided value`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Path <- "/some/path"
        let actual = pathPrefix "/startsWith" ctx
        isNone actual


    (*
        pathParse
    *)

    [<TestMethod>]
    member __.``pathParse returns Some if the request matches the provided parser`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Path <- "/some/path"
        let actual = pathParse (s "some" </> s "path") Some ctx
        isSome actual

    [<TestMethod>]
    member __.``pathParse returns None if the request does not match the provided parser`` () =
        let ctx = getContext ()
        let req = ctx.HttpContext.Features.Get<IHttpRequestFeature>()
        req.Path <- "/some/path"
        let actual = pathParse (s "some") Some ctx
        isNone actual


    (*
        authenticated
    *)

    [<TestMethod>]
    member __.``authenticated returns Some if the request has an authenticated user`` () =
        let ctx = getContext ()
        ctx.HttpContext.User <- userWithClaims true [("quit", "deed")]
        let actual = authenticated ctx
        isSome actual

    [<TestMethod>]
    member __.``authenticated returns None if the request does not have an authenticated user`` () =
        let ctx = getContext ()
        ctx.HttpContext.User <- userWithClaims false [("quit", "deed")]
        let actual = authenticated ctx
        isNone actual


    (*
        claimType
    *)

    [<TestMethod>]
    member __.``claimType returns Some if the request user has the provided claim type`` () =
        let ctx = getContext ()
        ctx.HttpContext.User <- userWithClaims true [("quit", "deed")]
        let actual = claimType "quit" ctx
        isSome actual

    [<TestMethod>]
    member __.``claimType returns None if the request does not have the provide claim type`` () =
        let ctx = getContext ()
        ctx.HttpContext.User <- userWithClaims false [("quit", "deed")]
        let actual = claimType "unsubstantiated" ctx
        isNone actual


    (*
        claim
    *)

    [<TestMethod>]
    member __.``claim returns Some if the request user has the provided claim type and value`` () =
        let ctx = getContext ()
        ctx.HttpContext.User <- userWithClaims true [("quit", "deed")]
        let actual = claim "quit" "deed" ctx
        isSome actual

    [<TestMethod>]
    member __.``claim returns None if the request does not have the provide claim type and value`` () =
        let ctx = getContext ()
        ctx.HttpContext.User <- userWithClaims false [("quit", "deed")]
        let actual = claim "unsubstantiated" "deed" ctx
        isNone actual


    
