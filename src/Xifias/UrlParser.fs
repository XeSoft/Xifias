namespace Xifias

// ported from https://github.com/evancz/url-parser
// under BSD 3-clause license

[<AutoOpen>]
module UrlParser =

    open Microsoft.AspNetCore.Http


    type State<'a> =
        {
            Visited : string list
            Unvisited : string list
            Params : IQueryCollection
            Value : 'a
        }


    type Parser<'a, 'b> =
        | Parser of (State<'a> -> State<'b> list)


    type QueryParser<'a, 'b> =
        | QueryParser of (State<'a> -> State<'b> list)


    module Internal =


        let toOption r =
            match r with
                | Error _ ->
                    None

                | Ok value ->
                    Some value


        let intParse s =
            match System.Int32.TryParse s with
                | false, _ ->
                    Error "not an integer"

                | true, i ->
                    Ok i


        let guidParse s =
            match System.Guid.TryParse s with
                | false, _ ->
                    Error "not a guid"

                | true, guid ->
                    Ok guid


        let getParam s (collection : IQueryCollection) =
            match collection.TryGetValue s with
                | false, _ ->
                    None

                | true, sv ->
                    Some (sv.ToString())


        let mapHelp f { Visited = visited; Unvisited = unvisited; Params = ps; Value = value } =
            {
                Visited = visited
                Unvisited = unvisited
                Params = ps
                Value = f value
            }


        let paramHelp f stringOpt =
            match stringOpt with
                | None ->
                    None

                | Some value ->
                    f value |> toOption


        let rec parseHelp states =
            match states with
                | [] ->
                    None

                | state :: rest ->
                    match state.Unvisited with
                        | [] ->
                            Some state.Value

                        | [ "" ] ->
                            Some state.Value

                        | _ ->
                            parseHelp rest


        let splitUrl (s : string) =
            match List.ofArray (s.Split('/')) with
                | "" :: segments ->
                    segments

                | segments ->
                    segments

    
    (*
        Path parsing
    *)


    let custom toResult =
        Parser
            (
                fun { Visited = visited; Unvisited = unvisited; Params = ps; Value = value } ->
                    match unvisited with
                        | [] ->
                            []

                        | next :: rest ->
                            match toResult next with
                                | Ok nextValue ->
                                    [ {
                                        Visited = (next :: visited)
                                        Unvisited = rest
                                        Params = ps
                                        Value = value nextValue
                                    } ]

                                | Error _ ->
                                    []
            )


    let string<'a> : Parser<string -> 'a, 'a> =
        custom (fun s -> if s = "" then Error "empty string" else Ok s)


    let int<'a> : Parser<int -> 'a, 'a> =
        custom Internal.intParse


    let guid<'a> : Parser<System.Guid -> 'a, 'a> =
        custom Internal.guidParse


    (*
        Parameter parsing
    *)

    let s (st : string) =
        Parser
            (
                fun { Visited = visited; Unvisited = unvisited; Params = ps; Value = value } ->
                    match unvisited with
                        | [] ->
                            []

                        | next :: rest ->
                            if next = st then
                                [ {
                                    Visited = (next :: visited)
                                    Unvisited = rest
                                    Params = ps
                                    Value = value
                                } ]

                            else
                                []
            )


    let (</>) (Parser parseBefore) (Parser parseAfter) =
        Parser
            (
                fun state ->
                    List.collect parseAfter (parseBefore state)
            )


    let map subValue (Parser parse) =
        Parser
            (
                fun { Visited = visited; Unvisited = unvisited; Params = ps; Value = value } ->
                    List.map (Internal.mapHelp value)
                        (parse
                            {
                                Visited = visited
                                Unvisited = unvisited
                                Params = ps
                                Value = subValue
                            }
                        )
            )


    let (<?>) (Parser parser) (QueryParser queryParser) =
        Parser
            (
                fun state ->
                    List.collect queryParser (parser state)
            )


    let customParam key f =
        QueryParser
            (
                fun { Visited = visited; Unvisited = unvisited; Params = ps; Value = value } ->
                    [ {
                        Visited = visited
                        Unvisited = unvisited
                        Params = ps
                        Value = (value (f (Internal.getParam key ps)))
                    } ]
            )


    let stringParam name =
        customParam name id


    let intParam name =
        customParam name (Internal.paramHelp Internal.intParse)


    let guidParam name =
        customParam name (Internal.paramHelp Internal.guidParse)


    (*
        Running a parse
    *)

    
    let parse (Parser parser) ps url =
        Internal.parseHelp
            ( parser
                (
                    {
                        Visited = []
                        Unvisited = Internal.splitUrl url
                        Params = ps
                        Value = id
                    }
                )
            )


    let parseFromContext parser (context : HttpContext) =
        parse parser context.Request.Query (context.Request.Path.ToString())
