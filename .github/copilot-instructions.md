# Copilot Instructions

## Project Guidelines
- For xUnit `[InlineData]` with nullable decimal tests, use `double?` inputs and cast to `decimal?` inside the test to avoid binding issues.
- When adding new tests, ensure all referenced helper types (e.g., Node/NodeList) are defined in the same test class or moved to an existing test file where those types already exist.

## General Guidelines
- Avoid using phrases like "take a deep breath" in responses.
- When evaluating parser buffer helpers, explicitly distinguish normal state from EOF rollback state (e.g., BufferReadTillEnd) to avoid contradictory guidance.