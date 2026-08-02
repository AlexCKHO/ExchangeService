---
status: accepted (deliberately temporary — expected to be superseded)
---

# Sequencer transports a typed Order struct, not an opaque command envelope

The Sequencer's only real responsibilities are assigning a total order and making it durable, which argues for an opaque `{ CommandType, InstrumentId, Payload }` envelope that only the matching engine decodes — keeping order semantics out of the Sequencer and out of the WAL format entirely. We are nonetheless keeping the typed `Order` struct for now, extended with a command discriminator so that cancels can be sequenced on the same wire, and intend to migrate to an envelope later.

## Consequences

Until the migration happens, every new command shape (stop, iceberg, amend, mass-cancel) touches three places: the `Order` struct, the WAL record layout, and the replay path. The WAL's `Version` field is what makes the migration survivable — records written under the typed layout must remain replayable after the envelope lands, or the durability guarantee in `README.md` only holds until the next format change.
