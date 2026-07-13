---
name: csharp-async
description: >
  C# async/await best-practice checklist. Make sure to use this skill when writing or
  reviewing any async method, Task-returning API, or ReactiveCommand.CreateFromTask
  body, and when debugging deadlocks, unawaited tasks, sync-over-async, or
  cancellation problems — Rentier requires fully async I/O end-to-end, so most
  non-trivial code changes here qualify.
---

# C# Async Programming Best Practices

Apply these rules when writing or reviewing asynchronous C# code. In Rentier, all
I/O is async end-to-end with no `.Result`/`.Wait()` anywhere — these are project
rules, not preferences.

## Naming Conventions

- Use the 'Async' suffix for all async methods
- Match method names with their synchronous counterparts when applicable (e.g., `GetDataAsync()` for `GetData()`)

## Return Types

- Return `Task<T>` when the method returns a value
- Return `Task` when the method doesn't return a value
- Consider `ValueTask<T>` for high-performance scenarios to reduce allocations
- Avoid returning `void` for async methods except for event handlers

## Exception Handling

- Use try/catch blocks around await expressions
- Avoid swallowing exceptions in async methods
- Use `ConfigureAwait(false)` when appropriate to prevent deadlocks in library code
- Propagate exceptions with `Task.FromException()` instead of throwing in async Task returning methods

## Performance

- Use `Task.WhenAll()` for parallel execution of multiple tasks
- Use `Task.WhenAny()` for implementing timeouts or taking the first completed task
- Avoid unnecessary async/await when simply passing through task results
- Consider cancellation tokens for long-running operations

## Common Pitfalls

- Never use `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()` in async code
- Avoid mixing blocking and async code
- Don't create async void methods (except for event handlers)
- Always await Task-returning methods

## Implementation Patterns

- Implement the async command pattern for long-running operations
- Use async streams (IAsyncEnumerable<T>) for processing sequences asynchronously
- Consider the task-based asynchronous pattern (TAP) for public APIs

When reviewing C# code, identify violations of these practices and suggest concrete fixes.