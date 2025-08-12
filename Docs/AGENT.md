# Technical Brief
This document explains the overarching objective for this project and provides guidelines for execution.

## Objectives
I want to understand all of the semantic-kernal concepts/techniques by progressively introducing them into a demonstration application.

The order of additions is important: I want to start small and progressively add on top - or horizontally - in layers/steps.

I want to do this in the context of a user facing chat application so that we have a way to interact with agents and incrementally add features such as chat, file handling, multi-agent chat, search, vector stores, processes and so on.

## Technical Choices
- We are building a modern .net Blazor server application.
- We will be using SignalR for client/server interaction
- We will be using Tailwind styling via CDN
- We must limit the use of JsInterop and favour server side control. Only fallback to JsInterop if server side control is too problematic.
- We will use dotnet/C# best practices where ever possible.

## Style Guide
- do not use emojis - ever
- favour functional style over imperitive (but be pragmatic)
- all commit messages must use [conventional commits](https://www.conventionalcommits.org/en/v1.0.0/#specification)
- favour primary contructor pattern
- always document classes and interfaces

## Semantic Kernel Examples
As we progress, we should make ourselves familiar with how Semantic Kernel does stuff. Each of the [concepts](./CONCEPT_PHASES.md) are lifted from a [project we have locally](../../semantic-kernel/dotnet/samples/Concepts/) along with various [GettingStared{With...}](../../semantic-kernel/dotnet/samples/) in the samples directory.


## Ways of working
- We will follow a [ROADMAP](./ROADMAP.md)
- The roadmap is our record of what we are doing, what we have done and what needs to be done next.
- The roadmap must be kept up to date.
- We will work in small increments addressing one task/concept at a time.
- Each task we undertake will broadly follow this flow:
  1. Understand the task - restate our objectives
  2. Plan the work by breaking down the task down into logical implementation steps
  3. Seek review of the plan
  - adjust as necessary
  4. Seek approval to work on the next step
  5. Do the work
  6. Seek review at the end of work for the current step
  7. Repeat 4-6 until the task/feature is completed
  8. Update the roadmap
  9. Commit the work (we are using git)
