# Windows Phone 8.1 Store: what the client asks for, and the documents it parses

Everything below was measured against a Nokia Lumia 625 (RM-941) running Windows Phone 8.1
Update 2: `os=8.10.14219.0`, `hw=402699266`, `dm=RM-941_eu_france_308`, `cc=IT`.

The method matters as much as the numbers. A lab on a PC hijacks DNS for the Store hosts and
terminates TLS for them, so it **serves** these documents rather than observing the real
service. Every request line, query and header below is the device's; every *response* is
either the lab's own or an archived one. The origin is gone —
`marketplaceedgeservice.windowsphone.com` and `catalog.zune.net` are NXDOMAIN, and
`cdn.marketplaceedgeservice.windowsphone.com` still answers `503 - DNS failure` from Akamai —
so no capture of the real service exists in this work.

Raw evidence, tooling and the per-claim limits live in the lab repository,
[`vincenzosco/WindowsStoreRevivalForWP81`](https://github.com/vincenzosco/WindowsStoreRevivalForWP81)
(`docs/FINDINGS.md`). This page is the part of it that is useful to a revival project.

## Service configuration: the first thing the client asks for

```
GET /v8/Configuration/Stores/OEMStores/{OEMID}?mpcVersion=…&os=…&hw=…&dm=…
GET /v8/Configuration/Stores/MOStores/{MOID}
```

on `marketplaceedgeservice.windowsphone.com` — four requests each, once per boot, and not
again after a reboot.

The client's own endpoint table says what those URLs are. It is one `<ep>` entry inside the
signed retail FFU `RM941_3058.50000.1445.1005` (offset 1177075854):

```
<ep id="OEMStoreFile" url="{WPAppsMarketplaceShopSecure}v8/Configuration/Stores/OEMStores/{DeviceRawOEMID}?mpcVersion={MarketplaceClientVersion}&os=...&hw={DeviceHardwareBitmask}&dm={DeviceModelName}" />
<ep id="MobileOperatorStoreFile" url="…MOStores/{LastFetchedMOID}…" />
```

so `hw`, `dm` and `mpcVersion` come from the template, not from one handset's habits.

The reply is a `ConfigurationFile`, in the grammar the same file carries:

```xml
<ConfigurationFile version="81000" forceupdate="0">
  <Domains>
    <Domain id="PROD">
      <setting id="WPAppsMarketplaceShopSecure" nexus="WPAppsMarketplaceShopSecure">https://marketplaceedgeservice.windowsphone.com/</setting>
      …
```

Five domains exist in the build — `PROD`, `DF`, `INT`, `PartnerNet`, `CertNet` — and `PROD` is
the one the phone uses. Its settings are the placeholders the endpoint table substitutes:
`WPAppsMarketplaceShop` (`http://marketplaceedgeservice.windowsphone.com/`),
`WPAppsMarketplaceShopSecure` (`https://…`), `WPAppsMarketplaceShopCdn`
(`http://cdn.marketplaceedgeservice.windowsphone.com/`), `WPAppsMarketplaceImageService`
(`http://cdn.marketplaceimages.windowsphone.com/`), `Accounts` / `Buy` / `Review` / `Telemetry`
on the same edge host, `WPAppsMarketplaceTunerService`
(`https://marketplaceconfigservice.windowsphone.com/`), and `MoServices`
(`https://disco.moservices.microsoft.com/`).

Two things to know before implementing it:

- The store identity values — `OEMStoreID`, `MOStoreID`, `MOUniqueID`, `CatalogLocale`,
  `MarketplaceClientVersion` — are **not** in `PROD`. The build lists them as
  `EndpointParameter`s, values the client supplies per call, which is consistent with the
  client asking these two endpoints at start-up in order to learn them.
- Answering with a host you do not control is worse than omitting the setting. The client will
  use it, and the failure then happens somewhere the document does not name — images, or a
  purchase. The lab omits `TunerService` for exactly that reason.

## The endpoint table: the URL templates behind those settings

The same FFU block that carries the settings is a table of `<ep>` entries, each a URL template with `{Placeholder}`s the
client fills from the configuration and from its own request. The four that matter for a Store implementation, quoted from
`3058.50000.1445.1005` (@1177094311):

```
<ep id="Application"        url="{WPAppsMarketplaceShop}v9/catalog/apps/{objectid}?os={FullPhoneVersion}&amp;cc={DeviceUserGeoIDCountry}&amp;lang={DeviceUserDefaultUILanguage}&amp;hw={DeviceHardwareBitmask}&amp;dm={DeviceModelName}&amp;oemId={DeviceRawOEMID}&amp;moId={MOUniqueID}&amp;cf={ContentFilter}{urltail}" />
<ep id="ApplicationSecure"  url="{WPAppsMarketplaceShopSecure}v9/catalog/apps/{objectid}?…" />
<ep id="ApplicationSearch"  url="{WPAppsMarketplaceShop}v9/catalog/apps?…&amp;chunkSize={appsearchchunksize}&amp;q={query}&amp;cf={ContentFilter}" />
<ep id="PrefixSearch"       url="{WPAppsMarketplaceAutoSuggest}v8/catalog/queries?…&amp;prefix={query}&amp;chunksize=4&amp;includeApplications={includeapplications}&amp;includeAlbums={includealbums}&amp;includeArtists={includeartists}&amp;includeTracks={includetracks}&amp;includePodcasts={includepodcasts}&amp;cf={ContentFilter}" />
```

Four things follow, and each of them costs an hour to rediscover:

- **`/v9/catalog/apps` is both the detail call and the search.** With `{objectid}` it is one app; with `q={query}` it is a
  result list. There is no separate search service.
- **`chunkSize` is a search parameter**, filled from `{appsearchchunksize}`. It is not a page-size field of the catalogue, so a
  client that never sends one is not refusing to page — it is not searching.
- **`/v8/catalog/queries` is the *suggestion* endpoint** (`PrefixSearch`) and its base is `WPAppsMarketplaceAutoSuggest`, which
  the shipped `PROD` domain points at the **CDN host on plain http**. Its `includeAlbums`, `includeArtists`, `includeTracks` and
  `includePodcasts` parameters are the Zune-generation media families, asked beside `includeApplications`.
- **`cf={ContentFilter}` and `{urltail}` ride every one of them**, and `urltail` is appended outside the query string, so a
  reimplementation that assumes it is a parameter will build a URL the client never asked for.

## The store page

```
GET http://cdn.marketplaceedgeservice.windowsphone.com/v9/catalog/storePages
      ?typeId=DynamicHub.Main&os=8.10.14219.0&cc=IT&lang=it-IT
      &hw=402699266&dm=RM-941_eu_france_308&oemId=NOKIA&moId=
```

This is the request the Store makes on every open: 67 of the 96 exchanges the measured handset
made in one capture, roughly every 35 seconds while the Store is in the foreground. The page is
selected by `typeId` (`DynamicHub.Main`), and the endpoint's own name for what it returns is a
**layout** — a page document, not a feed.

The frame, as the client's parser walks it (paths read out of `ZMarketplace.dll` and checked
against the same bytes): root `<StorePage>`, then `/Panes/Pane`, `/Templates/Template`, and the
entries under the atom feed nested inside it (`/feed/entry`). 27 paths are declared; 22 are
required.

```xml
<StorePage xmlns:a="…atom…" xmlns="…catalog…">
  <Panes>
    <Pane>
      <index>1</index>
      <Type>Collection</Type>
      <SubType/>
      <DataSource><Type>Applications</Type><Url>…</Url></DataSource>
      <BackgroundImage><id>…guid…</id></BackgroundImage>
      <CollectionId>…</CollectionId><MaxItems>…</MaxItems><MinItems>…</MinItems>
    </Pane>
  </Panes>
  <Templates><Template TEMPLATETYPE="003">…</Template></Templates>
  <a:feed>…<a:entry>…</a:entry>…</a:feed>
</StorePage>
```

The details that cost the most time to establish, and that a reimplementation will hit:

- **`/Panes/Pane` is absolute from the document element.** Panes placed inside `<a:feed>` are
  not found by that path; moving them there and re-reading the same path list is what showed it.
- **A pane's `/Type` is its own field; the feed it fetches is a different pair.**
  `/DataSource/Type` and `/DataSource/Url` are the `FeedType` and `Url` that a parser function
  named `Create StoreObjects with FeedType = %S Url = %S` takes. Writing the same value into
  both fields looks right on the wire and is wrong.
- **Four feed types exist**, each beside its own parser: `Applications`
  (`Marketplace_ParseAppsFeedIntoStoreObjects`), `FeaturedApps`
  (`…ParseFeedIntoStoreObjects`), `Collections`
  (`…ParseCollectionsFeedIntoStoreObjects`), `Categories`
  (`…ParseCategoriesFeedIntoStoreObjects`). A list is `<Type>Applications</Type>` plus a URL
  that answers that shape.
- **Template types are numeric.** `TEMPLATETYPE` values `000`, `003`, `004`, `015`, `017`; `003`
  is the hub's. The archived 2013 service used `<templateName>GamesHub</templateName>` instead,
  and this client refuses that document — see the error codes below.
- **The page carries a cookie.** The reply header `x-wp-storepage-cookie` (32 hex) is stored by
  the client at `HKLM\…\Microsoft\Zune\Settings\WPSStorePageCookie`, and later page requests
  carry it back. Whatever serves the page should be ready to mint and accept one, or to say why
  it does not.
- **The UI is built from pane plus template type, and it has an empty state.** The phone's own
  `ZuneAssets` carry `Store_HubCollectionUI_*`, `Store_HubFeaturedItemUI_*` and strings such as
  `ForYouEmptyListUI_NoResults_Test`: a hub draws a background pane and then one layout per
  pane, so a page that parses but yields no items draws as a background with nothing on it.

## What the client does after the page, and what it never does

Across 28 captured sessions the handset asked for the page 67 times and for a pane's
`DataSource` URL **zero** times. Requests carrying `chunkSize=25` or `category=` do exist in the
capture index, but every one of them is from the PC or from the second client on the handset,
not from the Store. The rest of an open is `GET /v8/catalog/queries?prefix=f` (search
suggestions), `POST /v8/catalog/updates` (once), `HEAD account.nokia.com`, and
`ssl-api.bing.com`.

When images are drawn, they are fetched as
`GET http://cdn.marketplaceimages.windowsphone.com/v8/images/{guid}?imagetype=icon_small` (also
`tile`, `background`, and others) — 420 of them in two and a half minutes after one list was
answered.

## Error codes you will meet while serving it

Three documents to the same URL, with the same request headers, produce three different
verdicts on the handset's screen:

| Document | Screen | Reading |
| --- | --- | --- |
| Truncated page (a missing `>`) | `0xc00cee23` | XML parse failure. The same document through `msxml6` on a PC gives `0xC00CE55F`, the same `0xC00C` facility. |
| A real 2013 `<a:feed>` hub page | `88675309` | The client got further and failed while drawing or validating it — a feed where a page was expected. Not a `0xC00C` code, and unchanged by replacing the template block with the numeric form. |
| A well-formed `<StorePage>` it accepts | nothing drawn, and nothing requested after it | Accepted and cached. This is the state to explain, not to celebrate: a page model that builds no items looks identical to a document that parses into nothing. |

Renaming a construct inside an accepted document (`StorePage` → `StorePageX`, `Template` →
`TemplateX`) does **not** produce an error. Silence therefore cannot be read as "the client
ignores this element"; a probe of this kind only reports constructs whose absence the client
refuses.

## What this does not establish

- No request below was answered by the real service. The shapes here are what this client
  *accepts*, never what the service *sent*.
- The configuration grammar comes from FFU `3058.50000.1445.1005`, while the measured device
  runs `8.10.14219.0`. A parser difference between builds is not excluded.
- Nothing here says how the client turns a parsed pane into items. The page parses, all 22
  required paths are present, and the list is still not drawn. That gap is a value-level
  question — `/Type`, `/SubType`, the page's own identity — and not a missing element.
