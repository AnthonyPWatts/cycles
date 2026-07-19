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

Move, attack, and colonise requests accept an optional `replacesOrderId` alongside their normal fleet and target fields. A fleet can have only one pending intention for the requested execution tick. `POST /orders/fleet/recall` accepts an owned outbound in-transit `fleetId`; its target is always the fleet's last occupied system and it cannot replace a different intention.

- Repeating the identical intention is idempotent and returns the existing pending order.
- A different intention without `replacesOrderId`, or with an ID that no longer identifies the current pending order, returns `409 Conflict` with `code: "stateConflict"`.
- A different intention with the current pending order ID creates the replacement and records the previous order as `superseded`.
- Historical order responses can include `supersededByOrderId`, linking the superseded record to the replacement.
- Order responses expose `commandSource` plus optional `sealedTick` and `sealedAt` fields. Human submissions are `human`; completed ledgers may also contain `gameAiPlanner`, `neutralPlanner`, and `implicitHold` records.
- Fleet responses expose an optional `departureTickNumber` alongside destination and arrival. Clients use the three values together for journey and recall timing; active fleets return all three as `null`.

Order outcome and fleet transit are separate state. A Move can be `processed` because dispatch succeeded while its fleet remains `inTransit` until the recorded arrival tick. Clients must keep that journey visible as an ongoing commitment rather than treating the historical order outcome as fleet readiness.

For example, replacing a pending move with an attack includes the order being confirmed for replacement:

```json
{
  "fleetId": "5ce2f146-1240-4175-a2d4-befd7895c20f",
  "targetFactionId": "372e73c0-0fb2-4770-94d6-c9d0833dc7c8",
  "replacesOrderId": "5a43a94a-d358-4ca7-8321-a00be6fd198b"
}
```

The confirmation ID makes a stale dashboard or competing submission fail safely instead of silently replacing a newer intention. `targetFactionId` is authoritative for attacks and permits neutral targets; `targetEmpireId` remains an optional compatibility input and response field for empire targets. `superseded` is an additive fleet-order status; clients must tolerate additive string-enum members as well as optional response fields.

## Cycle Stage And Turn Processing Contract

Cycle responses expose `turnStage`. The value describes command acceptance and the authoritative turn lifecycle:

| Value | Meaning |
| --- | --- |
| `commandOpen` | Human order, replacement, cancellation, and priority mutations may be accepted. |
| `closing` | Human commands are closed while internal planners fill their permitted intentions and the server admits or rejects complete Colonise reservation sets. |
| `sealed` | The complete ledger is immutable. No command source may append or replace an intention. |
| `resolving` | The server is processing the sealed ledger through the gameplay phases. |
| `publishing` | Outcomes are complete and the server is committing facts before the next command window. |

Order and priority mutations outside `commandOpen` fail with `409 Conflict` and `code: "stateConflict"`. SQL-backed tick execution commits in one transaction, so a normal client may see `commandOpen` on either side of a completed tick without observing each intermediate value. Clients must tolerate every documented stage and disable command submission once the value leaves `commandOpen`.

The dashboard bootstrap's `turnResolution.stage` mirrors `cycle.turnStage`, while `turnResolution.commandsAccepted` gives clients the corresponding mutation gate directly. Clients should use that flag to disable order, replacement, cancellation, Recall, priority, and Development closure controls; the API still enforces the stage independently.

Before sealing, each empire's otherwise-eligible Colonise orders are admitted as one reservation set. The closure budget is its Population stockpile plus projected current-turn Population income. A budget that cannot fund every order rejects the whole set before sealing; each rejected order exposes its durable `rejectionReason`, and the affected fleets receive implicit Holds. Cancellation and replacement during `commandOpen` change the set naturally.

The sealed ledger resolves as resource income; due construction; programme spending and construction starts; recalls, arrivals, movement, and Holds; combat; colonisation; derived state; next-window progression; then publication. Recall runs before passive arrival so a sealed last-turn reversal can prevent the destination arrival. Successful colonisation alone spends its reserved Population; a later eligibility failure leaves the amount unspent and cannot revive an order rejected at closure. That order is part of the gameplay contract. `createdAt`, `submittedAt`, response order, and event display order do not grant or report initiative. Clients should use the documented phases when explaining causality; timestamps cannot supply that meaning.

## Trusted Development Selection

When `Cycles:TrustedPlayerSelection:Enabled` is deliberately enabled, `GET /auth/trusted-players` returns active human accounts participating in the current match. `POST /auth/login` accepts only a listed `playerId`; arbitrary usernames, game-AI players, inactive accounts, missing participants, and forged session identifiers are rejected. Defeated or completed participants remain available for read-only inspection, but every mutation boundary rejects them. Outside the Development environment the API fails at startup unless the playground access code is configured, and the selector issues a protected, secure HttpOnly cookie. The selector is intended only for an access-restricted Development host and does not replace external identity or create accounts.

## Dashboard Bootstrap Contract

`GET /dashboard/bootstrap` supplies the dashboard's initial player view and normal refresh from one authoritative store snapshot. The typed response contains the authenticated session summary, active Cycle, empire, visible galaxy, owned fleets, selected-fleet detail, up to 50 pending or recent orders, visible Events, visible Chronicle entries, the visible opening briefing, and a player-scoped `turnResolution` presentation contract.

The Event collection keeps the latest visible tick complete. It then adds older visible Events up to the normal 100-record bound; a latest tick containing more than 100 visible Events remains complete rather than being cut in half.

The optional `selectedFleetId` query preserves the player's current fleet selection across a refresh. The server honours it only when the fleet belongs to the authenticated player's empire; a missing, stale, or foreign identifier falls back to the normal owned-fleet default without disclosing whether another fleet exists.

The bootstrap applies the same actor, empire, visibility, and fog-of-war rules as the narrower source endpoints. It does not return `GameState`, domain entities, another empire's fleets or orders, or hidden Events and Chronicle entries. The narrower endpoints remain available for focused interactions and future clients; the bootstrap is a read-optimised composition contract rather than their replacement.

### Turn-Resolution Presentation

`turnResolution` contains the following typed fields:

| Field | Contract |
| --- | --- |
| `cycleId`, `empireId`, `currentTickNumber`, `nextTickNumber` | Scope the presentation and forecast to the authenticated participant's current Cycle and empire. |
| `stage`, `stageLabel`, `stageDescription`, `commandsAccepted` | Describe the authoritative command-window stage and whether player mutations may be accepted. |
| `submissionTimeGrantsInitiative` | `false` under the current simultaneous-turn rules. Clients must not infer initiative from timestamps or collection order. |
| `playerPendingOrderCount` | Count of pending orders belonging to the authenticated empire. |
| `gamePendingHumanOrderCount` | Aggregate pending human-order count for the current Cycle. |
| `gameFleetIntentionCount` | Aggregate current-Cycle fleet-intention count for the ledger that closure will seal. |
| `forecast` | Empire-scoped planning projections and existing construction commitments. |
| `phases` | The complete ordered phase metadata used by Command and result presentation. |

The two game-wide counts are aggregate closure context for the Development confirmation. They do not expose another empire's fleet identifiers, order types, targets, timestamps, or outcomes, and they never include another Cycle.

`forecast` contains:

| Field | Meaning |
| --- | --- |
| `expectedIncome` | Projected Industry, Research, and Population from current phase-start influence. |
| `colonisationReservation` | Current eligible Colonise order count, Population required, Population available after projected income, and whether the complete set is funded. |
| `automaticMilitaryProgramme` | Current Military weight plus projected Industry spend, ship count, and delivery tick. |
| `scheduledDeliveries` | Ships already queued for delivery, grouped by tick with the Industry committed when construction started. |
| `surveyProjectionExpectedNextWindow` | Whether current Research plus projected income reaches the first doctrine threshold for the next command window. |
| `hasScheduledEffects` | Whether the forecast, existing delivery queue, or ongoing journey contains an automatic effect even when the player has submitted no new order. |

Expected income, Colonise reservation, automatic programme spending, new ship starts, and progression are projections from the current authoritative snapshot. A player can still change permitted orders or priorities before closure, and the resolver rechecks authoritative state. `scheduledDeliveries` describes construction that an earlier tick already committed; clients should label it separately from projections. Forecast calculation does not reserve resources, submit orders, or mutate game state.

Each `phases` item contains `order`, `phase`, `title`, and `consequence`. The response always supplies the nine current phase values in gameplay order: `resourceIncome`, `dueConstruction`, `programmeSpending`, `recallArrivalsAndMovement`, `combat`, `colonisation`, `derivedState`, `nextWindowProgression`, and `publication`. Titles and consequence copy are presentation text; `phase` and `order` are the stable fields for grouping.

### Event Phase Metadata

Player Event responses add nullable `resolutionPhase` and `resolutionPhaseOrder` fields. Resolution facts map to the same phase enum and order supplied in `turnResolution.phases`. Command-window, lifecycle, and other operational Events that have no resolution-phase meaning return `null`; clients must keep them visible without inventing a gameplay position. The dashboard groups Events by descending tick and ascending authoritative phase by default. Timestamp and severity sorts remain alternative views and do not alter causality.

## Visibility Contract

The complete galaxy topology remains public. Exact current system facts are visible where the authenticated empire has an active, non-empty fleet or where an empire in a current Alliance has one. Ending the Alliance removes that allied live visibility on the next read; Neutral, War, and Non-Aggression Pact relationships do not share it.

Alliance visibility does not pool fleet ownership, orders, resources, priorities, rankings, influence, or command authority. It creates no stale contact or destroyed-fleet memory. Development-admin visibility remains global.

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
