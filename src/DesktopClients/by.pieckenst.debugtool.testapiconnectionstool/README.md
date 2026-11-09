# Landscape Studio Diagnostics CLI

This console tool runs read-only diagnostics against the seven APIs, Ocelot, Identity login, and configured authorization scenarios. It can reach services through host-published ports or from the Docker Compose network using service DNS names. It does not print passwords or bearer tokens.

## Host mode

Start the API stack, then from `src`:

```powershell
dotnet run --project DesktopClients/by.pieckenst.debugtool.testapiconnectionstool -- check --target outside --compose
dotnet run --project DesktopClients/by.pieckenst.debugtool.testapiconnectionstool -- login --target outside --check-me
```

The login command prompts for a username and hidden password. For non-interactive use, set `LANDSCAPE_TEST_USERNAME` and `LANDSCAPE_TEST_PASSWORD` in the process environment. To test gateway routes using a token, set `LANDSCAPE_DEBUG_TOKEN` and run `check` again.

## Docker network mode

Build and run the tool as a Compose service in the same network as the APIs:

```powershell
docker compose --profile diagnostics run --rm serverdiagnostics check --target docker
docker compose --profile diagnostics run --rm serverdiagnostics suite --file /app/diagnostics.tests.json --target docker
```

Compose starts the API/gateway dependencies when needed. `--target docker` uses container DNS names such as `identity.api:8080` and `ocelotapigateway:8080`; use `--target outside` on the host for the published `localhost` ports.

## Login and role matrix

`diagnostics.tests.example.json` is a template for an existing administrator and an existing standard user. The standard user must already be provisioned with the `User` role. Supply credentials through environment variables; never put passwords in the JSON file:

```powershell
$env:IDENTITY_ADMIN_USERNAME = 'your-admin-name'
$env:IDENTITY_ADMIN_PASSWORD = 'your-admin-password'
$env:LANDSCAPE_USER_USERNAME = 'your-user-name'
$env:LANDSCAPE_USER_PASSWORD = 'your-user-password'
docker compose --profile diagnostics run --rm serverdiagnostics suite --file /app/diagnostics.tests.json --target docker
```

Edit a copy of the example profile to add API cases. Each case supports `method`, `path` (gateway-relative or absolute URL), `expectedStatus`, and optional `user` and JSON `body`. Mutating methods are not sent unless both `allowWrite: true` is set in that case and `--allow-write` is passed to the CLI; otherwise the case is reported as a failure. Use a disposable development/test environment for write tests.

## Other commands

```text
check [--target outside|docker] [--gateway URL] [--routes-file PATH] [--token-env NAME] [--compose]
login [--target outside|docker] [--username NAME] [--password-env NAME] [--check-me]
users --file users.json [--target outside|docker] [--allow-write]
suite --file tests.json [--target outside|docker] [--allow-write] [--timeout SECONDS]
request METHOD URL [--body JSON] [--token-env NAME] [--allow-write]
compose [--file docker-compose.yml]
```

`check` verifies every API `/health` and Swagger document and probes the GET routes declared in Ocelot. Service health endpoints include their configured database checks. `compose` verifies that the API and gateway containers are running. `suite` verifies direct and gateway login, expected user roles, and each configured status-code assertion. Exit code is `0` when all checks pass, `1` when a check fails, and `2` for invalid CLI/configuration or a missing local dependency.
