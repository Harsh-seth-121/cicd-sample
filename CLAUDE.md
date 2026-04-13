@.claude/rules/temporal_cicd_mermai_pack/


# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a CI/CD sample project in early development. The `.gitignore` is configured for .NET (C#), suggesting that is the intended tech stack. No source code, build system, or CI/CD pipelines have been added yet.

## Repository State

- Branch `init` contains the initial scaffold (README, .gitignore, LICENSE)
- Branch `main` is the target integration branch
- Remote: https://github.com/Harsh-seth-121/cicd-sample.git

## Expected Tech Stack

Based on `.gitignore` configuration:
- **.NET / C#** — MSBuild, NuGet packages
- Temporal Cloud
- Temporal dotnet SDK
- Likely **GitHub Actions** for CI/CD (given the GitHub remote)

## Getting Started (once source is added)

Build, test, and lint commands should be added here once the project scaffolding is in place. For a typical .NET project:

```sh
dotnet build
dotnet test
dotnet format
```

