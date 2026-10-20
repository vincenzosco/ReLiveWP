# Push on WP8.1: what the branches carry, what `main` already does, and what the image says

Written after reading every branch of this repository, then the WP8.1 retail image, with one question: **what
still has to be written for push to work, and where is the code that does it?**

The short answer is uncomfortable: the branch named `authentication` carries nothing that is not already in
`main`, the branch named `push` is older and smaller than what `main` ships, and the push service on `main`
already has an NSP listener, a store, presence routing and an Identity gRPC client. What follows is the
evidence for each of those three sentences, and the one thing this repository does not model at all — the
names the 8.x client itself uses.

## 1. The branches, measured

Computed on 2026-10-20 from `git rev-list --count`, both directions, for every `origin/wam/*` head:

| Branch | Ahead of `main` | Behind `main` | Last commit |
| --- | ---: | ---: | --- |
| `wam/authentication` | **0** | 151 | 2025-08-15 |
| `wam/eas` | 0 | 126 | 2025-08-29 |
| `wam/oneclick` | 0 | 130 | 2025-08-29 |
| `wam/ab` | 0 | 122 | 2026-05-23 |
| `wam/agps` | 0 | 108 | 2026-06-02 |
| `wam/push` | **6** | 110 | 2026-06-02 |
| `wam/zunenetapi` | **2** | 106 | 2026-06-04 |
| `wam/liveprofile` | 0 | 103 | 2026-06-07 |
| `wam/photo-sync` | **3** | 98 | 2026-06-29 |
| `wam/postgres` | 0 | 94 | 2026-07-02 |
| `wam/eas-vibe` | 0 | 57 | 2026-07-08 |
| `wam/wp81` | **2** | 55 | 2026-07-14 |
| `wam/exchange` | 0 | 53 | 2026-07-18 |
| `wam/eas-overhaul` | 0 | 39 | 2026-08-16 |
| `wam/internal-mail` | 0 | 31 | 2026-08-21 |
| `wam/sso` | 0 | 29 | 2026-08-23 |

Twelve of the sixteen carry **no commit that is not already in `main`**: they are stale labels, not work in
progress. Only four hold anything unique, and the largest of them is `wam/push`.

### `wam/authentication` is fully merged

Zero commits ahead, last touched 2025-08-15, 151 commits behind. Whatever "the authentication branch" means as
a place to look, it is not a source of missing code: everything it ever contained is in `main`, and the branch's
`protos/authentication.proto` is a three-RPC stub beside `main`'s eleven. The branch declares `Register`,
`GetSecurityTokens` and `VerifySecurityToken` and nothing else; `main` declares those three and eight more -
`RegisterDevice`, `CheckUserNameAvailability`, `RefreshSecurityTokens`, `VerifyPassword`,
`GetDeviceCertificate`, `VerifyDeviceCertificate`, `ChangePassword`, `RevokeDeviceTokens`. Re-deriving push
authentication from that branch would be re-deriving it from an older, smaller copy of what `main` has.

One correction to a plausible assumption, because the branch names invite it: the certificate RPCs live on
`wam/push`, not on `wam/authentication`. That branch's proto carries `Register`, `RegisterDevice`,
`GetSecurityTokens`, `VerifySecurityToken`, `GetDeviceCertificate` and `ValidateDeviceCertificate` - the shape
someone looking for "the push authentication flow" would expect, on the branch that also has the older push
service.

### `wam/push` is older than `main`, not ahead of it

Six commits ahead, but the push tree on that branch is the **pre-`src/services` layout** (`ReLiveWP.Services.Push/…`
at the repository root), and comparing the two trees file by file leaves only three paths that `main` does not
have:

```
Data/NotificationStatus.cs
Migrations/20260601014155_InitialPushStore.cs
Migrations/20260601014155_InitialPushStore.Designer.cs
```

The two migrations are the SQLite-era store; `main` has since moved to PostgreSQL
(`20260701210128_InitialPostgres`, then `20260701222243_DropTransientPushTables`). The enum is the only idea
worth keeping, and it is small:

```csharp
public enum NotificationStatus { Pending, InFlight, Delivered, Expired, Zombied }
```

`main`'s `QueuedNotification` has `StreamId`, `DeviceId`, `ChannelId`, `Payload`, `NotificationClass` and
`ExpiresAt` - and no status at all. Whether delivery states are wanted is a design decision, but it is the one
thing in the branch that is not simply superseded.

In the other direction, `main` has twelve files the branch never had: `Nsp/PresenceDirectory.cs`,
`Nsp/PushInstance.cs`, `Nsp/PushRouter.cs`, `PushMetrics.cs`, `Services/PresenceHeartbeatService.cs`,
`Services/PushRouteSubscriber.cs`, `Session/DeviceSequence.cs`, `Session/PushServerCertificate.cs`, and the two
PostgreSQL migration pairs. So the branch is not a fallback: it is a snapshot from before the service grew up.

## 2. What `main` already implements

Read from `src/services/ReLiveWP.Services.Push/Startup.cs`, so this is what the process actually wires:

- **The NSP listener** — `services.AddHostedService<PushTcpService>()`, with
  `PushServerCertificate` loading `Push:ServerCertPath` (`deploy/example/ReLiveWP.Services.Push.ini`:
  `/certs/push.pfx`) and `Push:ServerCertPassword` from a secret. The NSP message set is there in full:
  `NspConfigureRequest/Response`, `NspCreateChannelRequest/Response`, `NspDeregisterResponse`,
  `NspEndpointAck`, `NspNotification`, over `NspPackage`/`NspTlv`/`NspSession`/`NspCommand`/`NspTag`.
- **The PDU layer** — `Pdu/PDU.cs`, `PDUHeader.cs`, `PDUCommand`, `PDUHeaderType` and thirteen headers
  (`AuthenticateHeader`, `BindingHeader`, `DeviceInfoHeader`, `ErrorHeader`, `NetworkInfoHeader`,
  `OptimalKeepAliveHeader`, `SequenceHeader`, `SessionConfigHeader`, `SessionInfoHeader`, `SignatureHeader`,
  `TimestampHeader`, `TransportSessionConfigHeader`, `UnknownPDUHeader`).
- **Presence and routing** — `PushPresence`, `PresenceDirectory`, `PushInstance`, `PushRouter` as singletons,
  with `PushRouteSubscriber` and `PresenceHeartbeatService` as hosted services and Redis behind them.
- **The store** — EF Core on PostgreSQL (`ConnectionStrings:Push`), with `ChannelStore`, `NotificationQueue`,
  `SessionStore`, `DeviceSequence`, plus a `NotificationCleanupService` and a readiness check on the database.
- **Authentication** — `AddGrpcClient<Authentication.AuthenticationClient>` pointed at `Endpoints:Identity`.
  Push already asks Identity who a device is; it does not verify certificates itself.
- **HTTP** — `deploy/example/...ini` puts Kestrel on `http://0.0.0.0:10005`, which is the address
  `docs/addresses.txt` already reserves for `Push`.

That is a service, not a stub. The reimplementation the branch names suggest has already happened on `main`.

## 3. What the WP8.1 client itself says — and it is not NSP

New material, measured today against the retail image `RM941_3058.50000.1445.1005`
(1 611 661 312 bytes), with a byte search in **both** encodings (ASCII and UTF-16LE), every hit carrying its
absolute offset. This is a string search of a build, not a protocol reading: it says which names the client
carries, and nothing about what it does with them.

**Absent, and it matters:**

```
notify.live.net   -> 0 hits (ascii and utf16le)
mpns              -> 0 hits (ascii and utf16le)
```

The WP7-era push hosts are not baked into this image anywhere.

**Present, and it is a different vocabulary:**

```
@677591480 (utf16): … ID_CAP_BSS_PUSH_INTERFACE
                        BNSNotifyNabSyncMsg
                        BNSNotifyPushMsg
                        GetLightupStatus
                        GetWpnPhoneRegistrationEndpoint
                        RegisterWithPushChannel
                        UnregisterWithPushChannel …
@1008332561 (ascii): … E_BNS_SYSTEM_SPOT_FULL E_BNS_DUPLICATE_ITEM E_BNS_ITEM_NOT_FOUND …
                        szPushChannel … szRecurringIntervalInSeconds … _channelName
                        _pushRawMessage set_Digest _nabMessageHeader _nabMessageBody _nabMessageDigest …
```

Three things follow, each stated as narrowly as the evidence allows:

1. **The client's own names are WPN and BNS, not NSP/MPNS.** There is a *registration endpoint* it asks for
   (`GetWpnPhoneRegistrationEndpoint`) and channel operations it performs against it
   (`RegisterWithPushChannel`, `UnregisterWithPushChannel`), and there is a delivery path with its own message
   classes (`BNSNotifyPushMsg`, `BNSNotifyNabSyncMsg`) and error codes (`E_BNS_*`).
2. **Notification payloads travel with a NAB header, body and digest** (`_nabMessageHeader`, `_nabMessageBody`,
   `_nabMessageDigest`) - a framing this repository's NSP layer does not have, and one an 8.x client would
   expect.
3. **The host is not in the image**, which is consistent with an endpoint that arrives from registration
   rather than from a baked-in constant. It also means a revival cannot learn the push host from the firmware;
   it has to come from a capture.

The capability name `ID_CAP_BSS_PUSH_INTERFACE` is worth noting beside all of this: it is the capability an app
(or a component) declares to use the channel, and it is the kind of string that identifies which package owns
the flow if someone goes looking in the image.

## 4. What this suggests, and what it does not

What it suggests, in the order it would be worth doing:

- **Do not rebuild push from `wam/push`.** Its only unique asset is a status enum. If delivery states are
  wanted, add them to `main`'s `QueuedNotification` on their own merits.
- **Treat `wam/authentication` as merged** and read `main`'s proto instead; it is a superset.
- **The 8.x gap is registration, not delivery.** NSP is the delivery wire this repository models well; the
  names in the image point at a registration endpoint (`RegisterWithPushChannel`) and a NAB-framed payload,
  neither of which exists here. A real capture of a phone registering a channel would settle what
  `GetWpnPhoneRegistrationEndpoint` returns - and that is the one artefact this repository does not have.

What it does not say: that the WP8.1 client speaks NSP at all, or that it speaks *only* WPN/BNS; that the
`E_BNS_*` codes are the ones a client meets; or that `notify.live.net` was never used by an earlier build.
Zero hits in one image is zero hits in one image, and every offset above belongs to it.
