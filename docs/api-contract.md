# Player API Contract

The Cycles player API is a server-authoritative JSON interface for the browser dashboard and future clients. Player-facing endpoints return purpose-built response contracts rather than domain entities or persistence shapes.

## JSON Conventions

- Property names use `camelCase`.
- Enum values use `camelCase` strings.
- Numeric enum input is rejected.
- Dates and times use the normal `System.Text.Json` ISO 8601 representation.
- Successful responses may gain optional fields without requiring a new API version.

For example, a successful order response includes camelCase properties and a string enum:

```json
{
  "fleetOrderId": "5a43a94a-d358-4ca7-8321-a00be6fd198b",
  "orderType": "moveFleet",
  "status": "pending",
  "targetSystemId": "d960253b-23be-410d-8690-812fa524af19",
  "executeAfterTick": 4
}
```

Clients must not send numeric enum values such as `"orderType": 1`.

## Fleet Order Intent Contract

Move, attack, and colonise requests accept an optional `replacesOrderId` alongside their normal fleet and target fields. A fleet can have only one pending intention for the requested execution tick.

- Repeating the identical intention is idempotent and returns the existing pending order.
- A different intention without `replacesOrderId`, or with an ID that no longer identifies the current pending order, returns `409 Conflict` with `code: "stateConflict"`.
- A different intention with the current pending order ID creates the replacement and records the previous order as `superseded`.
- Historical order responses can include `supersededByOrderId`, linking the superseded record to the replacement.

For example, replacing a pending move with an attack includes the order being confirmed for replacement:

```json
{
  "fleetId": "5ce2f146-1240-4175-a2d4-befd7895c20f",
  "targetFactionId": "372e73c0-0fb2-4770-94d6-c9d0833dc7c8",
  "replacesOrderId": "5a43a94a-d358-4ca7-8321-a00be6fd198b"
}
```

The confirmation ID makes a stale dashboard or competing submission fail safely instead of silently replacing a newer intention. `targetFactionId` is authoritative for attacks and permits neutral targets; `targetEmpireId` remains an optional compatibility input and response field for empire targets. `superseded` is an additive fleet-order status; clients must tolerate additive string-enum members as well as optional response fields.

## Trusted Development Selection

When `Cycles:TrustedPlayerSelection:Enabled` is deliberately enabled, `GET /auth/trusted-players` returns active human accounts participating in the current match. `POST /auth/login` accepts only a listed `playerId`; arbitrary usernames, game-AI players, inactive accounts, missing participants, and forged session identifiers are rejected. Defeated or completed participants remain available for read-only inspection, but every mutation boundary rejects them. Outside the Development environment the API fails at startup unless the playground access code is configured, and the selector issues a protected, secure HttpOnly cookie. The selector is intended only for an access-restricted Development host and does not replace external identity or create accounts.

## Error Contract

Handled player-facing failures retain an appropriate HTTP status and return the same envelope:

```json
{
  "code": "validationFailed",
  "message": "Priority weights must total 100.",
  "details": null,
  "traceId": "0HNB4QJ4R5M2J:00000001"
}
```

- `code` is the stable machine-readable value on which clients may branch.
- `message` is a safe human-readable explanation which clients may display but must not parse.
- `details` is optional structured validation information.
- `traceId` is optional diagnostic correlation. It does not contain a stack trace or internal exception detail.

Current error codes are:

| HTTP status | Code | Meaning |
| --- | --- | --- |
| `401 Unauthorized` | `authenticationRequired` | The request has no accepted authenticated player. |
| `403 Forbidden` | `forbidden` | The authenticated player lacks authority for the operation or resource. |
| `400 Bad Request` | `validationFailed` | The request body or requested action is invalid. |
| `409 Conflict` | `stateConflict` | Valid input conflicts with the current authoritative state. |
| `404 Not Found` | `notFound` | The requested resource does not exist in the permitted scope. |

Unhandled server failures do not become additional documented client codes and must not expose exception types, stack traces, secrets, connection strings, or other internal diagnostics.

## Fact Presentation

Raw `FactJson` is internal flexible storage and is not part of ordinary player event, battle, or tick-result responses. History uses `displayText` and factual Chronicle summaries for normal presentation.

The Day One guide consumes the purpose-built `GET /briefings/opening` response. Its shape contains only the stable scenario, focus-system, and objective identifiers required by the guide:

```json
{
  "scenarioKey": "development-match-v2",
  "focusSystemId": "d960253b-23be-410d-8690-812fa524af19",
  "objectives": {
    "move": {
      "fleetId": "5ce2f146-1240-4175-a2d4-befd7895c20f",
      "targetSystemId": "8b812e45-362c-4b5a-b523-fc729982c7df"
    },
    "colonise": {
      "fleetId": "8ab7f07a-696a-47a8-a756-d385a1f5ab37",
      "systemId": "c2e9ed55-b4e5-4858-b525-40ac3b87ea95"
    },
    "attack": {
      "fleetId": "3ae815cc-2faf-4e51-b709-88a31cde5959",
      "systemId": "df7fe765-17ec-4ab9-8ca1-544e167ab681",
      "targetFactionId": "372e73c0-0fb2-4770-94d6-c9d0833dc7c8"
    }
  }
}
```

The endpoint applies the same authenticated-player, empire, Cycle, event-visibility, and fog-of-war rules as the source briefing fact. It returns JSON `null` when no visible briefing exists.

## Compatibility

The following changes are compatible:

- adding an optional response field;
- adding a new error code for a new failure case;
- adding a new endpoint.

The following require an explicit compatibility decision:

- renaming or removing a response field;
- changing an existing enum wire value;
- changing the meaning of an existing error code;
- making an optional field required;
- exposing an internal storage or domain shape as a player contract.

URL or media-type versioning is not required until a concrete compatibility need exists.
