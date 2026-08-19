# Data versioning design

## What it is

A **data version** is the short opaque label a query result carries so that a client asking for it again can be told "nothing has changed" instead of being sent the whole answer. On the wire it is an HTTP entity-tag: the response carries `ETag`, the client sends it back as `If-None-Match`, and a matching request is answered `304 Not Modified` with no body.

One value type carries it end to end: `DataVersion` in `src/ServiceControl.Persistence/Infrastructure/DataVersion.cs`. Every persister produces one, `QueryStatsInfo.Version` carries it out of the persistence layer, and the Web API turns it into the header. It is a `readonly struct`, so `default` is a legitimate value and no variable of the type can be null.

This is the primary (error) instance only. The audit instance still carries a `string ETag` on its own `QueryStatsInfo` and has not been converted.

## The one rule

**If any field the response body renders can change without the version changing, a client caches that page indefinitely, and nothing reveals it.** No log line, no exception, no failing test. Every design decision below follows from that asymmetry: a version that moves too often costs a redundant download, a version that moves too rarely serves wrong data.

So the version has to cover the response, not the data. Two requests that render different bodies must not share a validator, which is why paged endpoints name the page and not only the underlying set.

## Making one

| Factory                                 | Use it for                                                                                      |
| --------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `FromToken(string)` / `FromToken(long)` | a token the backend already produces, such as a RavenDB index etag or document change vector    |
| `Compose(terms)`                        | named terms over aggregates, where an aggregate provably moves with the fields it stands in for |
| `OverRows(summary, rows, fields)`       | a list the response renders row by row: summary terms for the whole set, plus one term per row  |
| `Combine(instances)`                    | one version for a result gathered from several instances                                        |
| `FromClient(header)`                    | a validator a caller sent back, in any shape an old or current instance might emit              |

`Compose` hashes its description through `DeterministicGuid.MakeId`, so the emitted tag is a GUID rather than the underlying values.

Term names and every field inside a row are **length prefixed**. Without that, free user text carrying a delimiter could make two different results digest identically: a failure group titled `x.y` with an empty `Type` would collide with one titled `x` whose `Type` is `y`. `Format` accepts strings, `bool`, `DateTime` and `DateTimeOffset` (both by ticks) and anything `IFormattable` under the invariant culture, and **throws** on anything else, because a type whose `ToString` is not a documented function of its content would pin the version silently.

`OverRows` names rows by position, so a caller whose query has no `ORDER BY` has to sort them first or the validator churns.

## Absence

`DataVersion.None` is `default`, and it **matches nothing, not even itself**. Two parties that both know nothing have not established that nothing changed, so an empty-string validator matching itself would answer `304` for every request.

Absence propagates in the safe direction. `WithEtag` writes no header for `None`, so no header means no `If-None-Match`, which means the full body. `Combine` returns `None` as soon as any instance reports none, rather than quietly claiming to cover an instance it could not see.

## Two comparisons, and they are not the same question

- `Matches(other)` is the cache question, and the only one a store or a conditional-request filter should ask. It requires both sides present.
- `Equals(other)` is ordinary value equality and stays reflexive, so `None.Equals(None)` is true and the struct behaves in a dictionary.

`operator ==` is deliberately left undefined so that choosing between them is explicit at the call site.

## Reaching the client

`WithEtag` emits **every** tag weak, as `W/"…"`. Nothing here can promise the response bytes: response compression rewrites them without touching the tag, and no endpoint enables range processing, which is the one thing an exact validator would buy. RFC 9110 requires `If-None-Match` to use the weak comparison anyway, so the marking costs nothing.

`NotModifiedStatusHttpHandler` turns a matching request into a `304`. It compares with `EntityTagHeaderValue.Compare(useStrongComparison: false)`, because `Equals` on that type compares strength as well as the tag and its own documentation says not to use it for this. `*` matches whenever a representation exists.

`GetKnownVersion` reads the caller's validator back through typed headers, not the raw header, because `If-None-Match` is a comma-separated list and the raw header hands the whole list over as one malformed value. A caller holding several validators, or the `*` wildcard, is treated as holding none: a store can only skip work for a single known version. The `304` still comes from the filter either way.

## Skipping the query

`GET /api/eventlogitems` is the **only** endpoint that hands the caller's version down to the persister: `IEventLogDataStore.GetEventLogItems` takes a `knownVersion`, and on a match returns `QueryResult.Unchanged` without fetching the page at all. Everywhere else the version is compared after the work is done and only the response body is saved.

That makes the coverage rule sharper here than anywhere else. A page-blind version does not merely serve a stale page, it means the right page is never queried. The EF store therefore names the page window (`page`, `pageSize`) rather than the rows, which is sound only because a row in that table never changes and the query has a total order, and which keeps the caller's version answerable without fetching anything.

## Across instances

Scatter-gather endpoints merge one version per instance through `Combine`. It is keyed on instance id and sorted ordinally, so the composite is independent of the order instances answered in but still moves if two instances swap which validator they report.

An API whose own instance holds none of the data drops its own empty result before aggregating, via `AggregateStatsFromRemotesOnly`. Left in, its version-less placeholder would take the whole composite to `None` and the endpoint would emit no tag at all.
