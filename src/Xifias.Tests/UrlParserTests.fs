namespace Xifias.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open Xifias.UrlParser
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Internal
open Xifias.Tests.Helpers
open Microsoft.Extensions.Primitives
open System.Collections.Generic


type TestRecord = { Stringy : string; Inty : int }


[<TestClass>]
type UrlParsing() =

    let q : IQueryCollection = new QueryCollection(0) :> IQueryCollection



    [<TestMethod>]
    member __.``parse ignores leading and trailing slashes`` () =
        let actual = parse (s "test") q "test" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "/test" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "test/" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "/test/" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual
        let actual = parse (s "test") q "/test/foo" |> Option.map (fun f -> f 1)
        areEqual None actual


    (*
        s parser
    *)

    [<TestMethod>]
    member __.``s parser returns Some when provided string matches`` () =
        let actual = parse (s "test") q "/test" |> Option.map (fun f -> f 1)
        areEqual (Some 1) actual

    [<TestMethod>]
    member __.``s parser returns None when provided string does not match`` () =
        let actual = parse (s "test") q "/grade" |> Option.map (fun f -> f 1)
        areEqual None actual


    (*
        string parser
    *)

    [<TestMethod>]
    member __.``string parser returns Some when provided string matches`` () =
        let actual = parse (string) q "/test"
        areEqual (Some "test") actual

    [<TestMethod>]
    member __.``string parser returns None when provided string part is empty`` () =
        let actual = parse (string) q "/"
        areEqual None actual


    (*
        int parser
    *)

    [<TestMethod>]
    member __.``int parser returns Some when provided string matches`` () =
        let actual = parse (int) q "/123"
        areEqual (Some 123) actual

    [<TestMethod>]
    member __.``int parser returns None when provided string does not have a number`` () =
        let actual = parse (int) q "/test"
        areEqual None actual


    (*
        guid parser
    *)

    [<TestMethod>]
    member __.``guid parser returns Some when provided string matches with no dashes`` () =
        let x = System.Guid.NewGuid()
        let actual = parse (guid) q ("/" + x.ToString("N"))
        areEqual (Some x) actual

    [<TestMethod>]
    member __.``guid parser returns Some when provided string matches with dashes`` () =
        let x = System.Guid.NewGuid()
        let actual = parse (guid) q ("/" + x.ToString())
        areEqual (Some x) actual

    [<TestMethod>]
    member __.``guid parser returns None when provided string does not have a guid`` () =
        let actual = parse (guid) q "/test"
        areEqual None actual


    (*
        custom parser
    *)

    [<TestMethod>]
    member __.``custom parser returns Some when provided string matches`` () =
        let float = custom (fun s -> match System.Double.TryParse s with | false, _ -> Error "Not a float" | true, v -> Ok v)
        let actual = parse (float) q "/123.1"
        areEqual (Some 123.1) actual

    [<TestMethod>]
    member __.``custom parser returns None when provided string does not have a number`` () =
        let float = custom (fun s -> match System.Double.TryParse s with | false, _ -> Error "Not a float" | true, v -> Ok v)
        let actual = parse (float) q "/test"
        areEqual None actual


    (*
        </> parser
    *)

    [<TestMethod>]
    member __.``</> parser return Some when provided string has 2 segments`` () =
        let actual = parse (s "test" </> string) q "/test/foo"
        areEqual (Some "foo") actual

    [<TestMethod>]
    member __.``</> parser returns None when provided string has 1 segment`` () =
        let actual = parse (s "test" </> string) q "/test"
        areEqual None actual


    (*
        map parser
    *)

    [<TestMethod>]
    member __.``map demo - turn parameters from parser into a record`` () =
        let createRecord a b = { Stringy = a; Inty = b }
        let urlParser = s "test" </> string </> s "id" </> int
        let recordParser = map createRecord urlParser
        let actual = parse recordParser q "/test/asdf/id/321"
        areEqual (Some (createRecord "asdf" 321)) actual


    (*
        stringParam parser
    *)

    [<TestMethod>]
    member __.``stringParam parser returns Some when provided query parameter is present`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues("value"))]))
        let actual = parse (s "test" <?> stringParam "key") q "/test" |> Option.bind id
        areEqual (Some "value") actual

    [<TestMethod>]
    member __.``stringParam parser returns Some when provided query parameter is present - blank version`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues(""))]))
        let actual = parse (s "test" <?> stringParam "key") q "/test" |> Option.bind id
        areEqual (Some "") actual

    [<TestMethod>]
    member __.``stringParam parser returns None when provided query parameter is not present`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>())
        let actual = parse (s "test" <?> stringParam "key") q "/test" |> Option.bind id
        areEqual None actual


    (*
        intParam parser
    *)

    [<TestMethod>]
    member __.``intParam parser returns Some when provided query parameter is present`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues("234"))]))
        let actual = parse (s "test" <?> intParam "key") q "/test" |> Option.bind id
        areEqual (Some 234) actual

    [<TestMethod>]
    member __.``intParam parser returns None when provided query parameter is present but not a number`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues("foo"))]))
        let actual = parse (s "test" <?> intParam "key") q "/test" |> Option.bind id
        areEqual None actual

    [<TestMethod>]
    member __.``intParam parser returns None when provided query parameter is not present`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>())
        let actual = parse (s "test" <?> intParam "key") q "/test" |> Option.bind id
        areEqual None actual


    (*
        guidParam parser
    *)

    [<TestMethod>]
    member __.``guidParam parser returns Some when provided query parameter is present - no dashes`` () =
        let x = System.Guid.NewGuid()
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues(x.ToString("N")))]))
        let actual = parse (s "test" <?> guidParam "key") q "/test" |> Option.bind id
        areEqual (Some x) actual

    [<TestMethod>]
    member __.``guidParam parser returns Some when provided query parameter is present with dashes`` () =
        let x = System.Guid.NewGuid()
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues(x.ToString()))]))
        let actual = parse (s "test" <?> guidParam "key") q "/test" |> Option.bind id
        areEqual (Some x) actual

    [<TestMethod>]
    member __.``guidParam parser returns None when provided query parameter is present but not a guid`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues("foo"))]))
        let actual = parse (s "test" <?> guidParam "key") q "/test" |> Option.bind id
        areEqual None actual

    [<TestMethod>]
    member __.``guidParam parser returns None when provided query parameter is not present`` () =
        let q = QueryCollection(new Dictionary<string, StringValues>())
        let actual = parse (s "test" <?> guidParam "key") q "/test" |> Option.bind id
        areEqual None actual


    (*
        customParam parser
    *)

    [<TestMethod>]
    member __.``customParam parser returns Some when provided query parameter is present`` () =
        let floatParam name = customParam name (Option.bind (fun s -> match System.Double.TryParse s with | false, _ -> None | true, f -> Some f))
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues("234.56"))]))
        let actual = parse (s "test" <?> floatParam "key") q "/test" |> Option.bind id
        areEqual (Some 234.56) actual

    [<TestMethod>]
    member __.``customParam parser returns None when provided query parameter is present but not a number`` () =
        let floatParam name = customParam name (Option.bind (fun s -> match System.Double.TryParse s with | false, _ -> None | true, f -> Some f))
        let q = QueryCollection(new Dictionary<string, StringValues>(dict [("key", StringValues("foo"))]))
        let actual = parse (s "test" <?> floatParam "key") q "/test" |> Option.bind id
        areEqual None actual

    [<TestMethod>]
    member __.``customParam parser returns None when provided query parameter is not present`` () =
        let floatParam name = customParam name (Option.bind (fun s -> match System.Double.TryParse s with | false, _ -> None | true, f -> Some f))
        let q = QueryCollection(new Dictionary<string, StringValues>())
        let actual = parse (s "test" <?> floatParam "key") q "/test" |> Option.bind id
        areEqual None actual


