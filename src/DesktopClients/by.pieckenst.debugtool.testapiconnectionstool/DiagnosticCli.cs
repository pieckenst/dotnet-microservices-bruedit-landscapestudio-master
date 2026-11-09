using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace by.pieckenst.debugtool.testapiconnectionstool;

internal static class DiagnosticCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly ServiceTarget[] LocalServices =
    [
        new("Identity API", "localhost", 8003),
        new("Account API", "localhost", 8004),
        new("Catalog API", "localhost", 8002),
        new("Project API", "localhost", 8001),
        new("Organization API", "localhost", 8005),
        new("Procurement API", "localhost", 8006),
        new("Analytics API", "localhost", 8000)
    ];

    private static readonly ServiceTarget[] DockerServices =
    [
        new("Identity API", "identity.api", 8080),
        new("Account API", "account.api", 8080),
        new("Catalog API", "catalog.api", 8080),
        new("Project API", "project.api", 8080),
        new("Organization API", "organization.api", 8080),
        new("Procurement API", "procurement.api", 8080),
        new("Analytics API", "analytics.api", 8080)
    ];

    private static readonly string[] ExpectedComposeServices =
    [
        "identity.api", "account.api", "catalog.api", "project.api",
        "organization.api", "procurement.api", "analytics.api", "ocelotapigateway"
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "check" => await CheckAsync(args[1..]),
                "login" => await LoginAsync(args[1..]),
                "users" => await UsersAsync(args[1..]),
                "suite" => await SuiteAsync(args[1..]),
                "request" => await RequestAsync(args[1..]),
                "compose" => await ComposeAsync(args[1..]),
                _ => Fail($"Unknown command '{args[0]}'. Run with --help for usage.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
    }

    private static async Task<int> CheckAsync(string[] args)
    {
        var target = GetTarget(args);
        var endpoints = GetEndpoints(target);
        var gateway = GetStringOption(args, "--gateway") ?? target.DefaultGateway;
        var routesFile = GetStringOption(args, "--routes-file") ?? FindRoutesFile();
        var timeout = GetIntOption(args, "--timeout", 5, 1, 120);
        var results = new List<CheckResult>();
        using var client = CreateClient(timeout);

        foreach (var service in endpoints)
        {
            var baseUrl = service.BaseUrl;
            results.Add(await ProbeAsync(client, $"{service.Name} health", Join(baseUrl, "/health"), [HttpStatusCode.OK]));
            results.Add(await ProbeAsync(client, $"{service.Name} OpenAPI", Join(baseUrl, "/swagger/v1/swagger.json"), [HttpStatusCode.OK]));
        }

        if (!File.Exists(routesFile))
        {
            results.Add(new CheckResult("Ocelot route inventory", false, null,
                $"Routes file not found: {routesFile}. Pass --routes-file."));
        }
        else
        {
            var token = Environment.GetEnvironmentVariable(GetStringOption(args, "--token-env") ?? "LANDSCAPE_DEBUG_TOKEN");
            foreach (var route in LoadGetRoutes(routesFile))
            {
                var path = ReplaceRouteParameters(route.Path);
                var url = Join(gateway, path);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var result = await ProbeRequestAsync(client, $"Gateway GET {route.Path}", request,
                    token is null && route.RequiresAuthentication
                        ? [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]
                        : [HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.NoContent,
                            HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
                            HttpStatusCode.MethodNotAllowed, HttpStatusCode.Conflict,
                            HttpStatusCode.UnprocessableEntity]);
                if (result.StatusCode is 502 or 503 or 504)
                    result = result with { Passed = false, Detail = "Gateway could not reach its downstream service." };
                results.Add(result);
            }
        }

        if (args.Contains("--compose", StringComparer.OrdinalIgnoreCase))
            results.Add(await CheckComposeAsync(GetStringOption(args, "--compose-file") ?? FindComposeFile()));

        PrintResults(results, args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        if (!args.Contains("--json", StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"\n{results.Count(result => result.Passed)}/{results.Count} checks passed for target '{target.Name}'.");
        return results.All(result => result.Passed) ? 0 : 1;
    }

    private static async Task<int> LoginAsync(string[] args)
    {
        var target = GetTarget(args);
        var username = GetStringOption(args, "--username") ?? Environment.GetEnvironmentVariable("LANDSCAPE_TEST_USERNAME") ?? Prompt("Username: ");
        var passwordEnvironment = GetStringOption(args, "--password-env") ?? "LANDSCAPE_TEST_PASSWORD";
        var password = Environment.GetEnvironmentVariable(passwordEnvironment) ?? ReadSecret($"Password ({passwordEnvironment} is unset): ");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return Fail("Username and password must not be empty.");

        var result = await AuthenticateAsync(CreateClient(20), target, username, password);
        Console.WriteLine($"Identity direct login: {FormatStatus(result.DirectStatus)}");
        Console.WriteLine($"Gateway login:         {FormatStatus(result.GatewayStatus)}");
        if (result.Token is null)
        {
            Console.WriteLine("Login failed. Response bodies and credentials are suppressed.");
            return 1;
        }

        Console.WriteLine($"Login succeeded for '{username}', role: {result.Role ?? "unknown"}.");
        if (args.Contains("--check-me", StringComparer.OrdinalIgnoreCase))
        {
            var meDirect = await AuthenticatedGetAsync(CreateClient(20), Join(target.Identity.BaseUrl, "/api/v1/auth/me"), result.Token);
            var meGateway = await AuthenticatedGetAsync(CreateClient(20), Join(target.DefaultGateway, "/Auth/me"), result.Token);
            Console.WriteLine($"Identity /me: {(int)meDirect} {meDirect}");
            Console.WriteLine($"Gateway /Auth/me: {(int)meGateway} {meGateway}");
            return meDirect == HttpStatusCode.OK && meGateway == HttpStatusCode.OK &&
                   result.DirectStatus == HttpStatusCode.OK && result.GatewayStatus == HttpStatusCode.OK ? 0 : 1;
        }

        return result.DirectStatus == HttpStatusCode.OK && result.GatewayStatus == HttpStatusCode.OK ? 0 : 1;
    }

    private static async Task<int> UsersAsync(string[] args)
    {
        var file = GetStringOption(args, "--file");
        if (file is null) return Fail("users requires --file users.json.");
        var profile = await ReadProfileAsync(file);
        if (profile.Users.Count == 0) return Fail("The users file contains no users.");

        var target = GetTarget(args);
        var results = new List<CheckResult>();
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var client = CreateClient(20);
        foreach (var user in profile.Users)
        {
            var username = GetUsername(user);
            var password = Environment.GetEnvironmentVariable(user.PasswordEnv);
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                results.Add(new CheckResult($"Login {user.Name}", false, null,
                    string.IsNullOrWhiteSpace(username) ? $"Set {user.UserNameEnv} or userName in the profile." : $"Environment variable {user.PasswordEnv} is not set."));
                continue;
            }

            var auth = await AuthenticateAsync(client, target, username, password);
            var passed = auth.DirectStatus == HttpStatusCode.OK && auth.GatewayStatus == HttpStatusCode.OK && auth.Token is not null;
            results.Add(new CheckResult($"Login {user.Name}", passed, ToStatusCode(auth.GatewayStatus),
                passed ? $"authenticated; API role is {auth.Role ?? "unknown"}" : "login failed (response details suppressed)"));
            if (auth.Token is null) continue;

            tokens[user.Name] = auth.Token;
            var expectedRole = user.ExpectedRole;
            var actualRole = ReadJwtRole(auth.Token) ?? auth.Role;
            if (!string.IsNullOrWhiteSpace(expectedRole))
                results.Add(new CheckResult($"Role {user.Name}", string.Equals(expectedRole, actualRole, StringComparison.OrdinalIgnoreCase), null,
                    $"expected {expectedRole}; received {actualRole ?? "no role claim"}"));

            var directMe = await AuthenticatedGetAsync(client, Join(target.Identity.BaseUrl, "/api/v1/auth/me"), auth.Token);
            var gatewayMe = await AuthenticatedGetAsync(client, Join(target.DefaultGateway, "/Auth/me"), auth.Token);
            results.Add(new CheckResult($"Authenticated identity {user.Name}", directMe == HttpStatusCode.OK && gatewayMe == HttpStatusCode.OK,
                (int)gatewayMe, $"direct /me: {(int)directMe}; gateway /Auth/me: {(int)gatewayMe}"));
        }

        if (profile.Tests.Count > 0)
            await RunCasesAsync(client, target, profile.Tests, tokens, args.Contains("--allow-write", StringComparer.OrdinalIgnoreCase), results);

        PrintResults(results, args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        return results.All(item => item.Passed) ? 0 : 1;
    }

    private static async Task<int> SuiteAsync(string[] args)
    {
        var file = GetStringOption(args, "--file");
        if (file is null) return Fail("suite requires --file tests.json.");
        var profile = await ReadProfileAsync(file);
        if (profile.Users.Count == 0) return Fail("The suite must define at least one test user.");

        var target = GetTarget(args);
        var results = new List<CheckResult>();
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var client = CreateClient(GetIntOption(args, "--timeout", 30, 1, 300));
        foreach (var user in profile.Users)
        {
            var username = GetUsername(user);
            var password = Environment.GetEnvironmentVariable(user.PasswordEnv);
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                results.Add(new CheckResult($"Login {user.Name}", false, null,
                    string.IsNullOrWhiteSpace(username) ? $"Set {user.UserNameEnv} or userName in the profile." : $"Environment variable {user.PasswordEnv} is not set."));
                continue;
            }

            var auth = await AuthenticateAsync(client, target, username, password);
            var loggedIn = auth.DirectStatus == HttpStatusCode.OK && auth.GatewayStatus == HttpStatusCode.OK && auth.Token is not null;
            results.Add(new CheckResult($"Login {user.Name}", loggedIn, ToStatusCode(auth.GatewayStatus),
                loggedIn ? $"authenticated; API role is {auth.Role ?? "unknown"}" : "login failed (response details suppressed)"));
            if (auth.Token is null) continue;

            tokens[user.Name] = auth.Token;
            if (!string.IsNullOrWhiteSpace(user.ExpectedRole))
            {
                var actualRole = ReadJwtRole(auth.Token) ?? auth.Role;
                results.Add(new CheckResult($"Role {user.Name}", string.Equals(user.ExpectedRole, actualRole, StringComparison.OrdinalIgnoreCase), null,
                    $"expected {user.ExpectedRole}; received {actualRole ?? "no role claim"}"));
            }
        }

        await RunCasesAsync(client, target, profile.Tests, tokens,
            args.Contains("--allow-write", StringComparer.OrdinalIgnoreCase), results);
        PrintResults(results, args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        if (!args.Contains("--json", StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"\n{results.Count(item => item.Passed)}/{results.Count} tests passed.");
        return results.All(item => item.Passed) ? 0 : 1;
    }

    private static async Task RunCasesAsync(HttpClient client, Target target, IReadOnlyList<ApiTestCase> cases,
        IReadOnlyDictionary<string, string> tokens, bool allowWrite, ICollection<CheckResult> results)
    {
        foreach (var test in cases)
        {
            if (!TryParseMethod(test.Method, out var method))
            {
                results.Add(new CheckResult(test.Name, false, null, $"Unsupported HTTP method '{test.Method}'."));
                continue;
            }

            var isWrite = method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options;
            if (isWrite && (!allowWrite || !test.AllowWrite))
            {
                results.Add(new CheckResult(test.Name, false, null,
                    "Not sent: write tests require --allow-write and allowWrite=true in the test case."));
                continue;
            }

            if (!Uri.TryCreate(test.Path, UriKind.Absolute, out var uri))
            {
                if (!test.Path.StartsWith('/'))
                {
                    results.Add(new CheckResult(test.Name, false, null, "Path must start with '/' or be an absolute URL."));
                    continue;
                }
                uri = new Uri(Join(target.DefaultGateway, test.Path));
            }
            if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new CheckResult(test.Name, false, null, "Only HTTP and HTTPS test URLs are supported."));
                continue;
            }

            using var request = new HttpRequestMessage(method, uri);
            if (!string.IsNullOrWhiteSpace(test.User))
            {
                if (!tokens.TryGetValue(test.User, out var token))
                {
                    results.Add(new CheckResult(test.Name, false, null, $"User '{test.User}' has no valid login token."));
                    continue;
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            if (test.Body is not null)
            {
                try { using var _ = JsonDocument.Parse(test.Body); }
                catch (JsonException exception)
                {
                    results.Add(new CheckResult(test.Name, false, null, $"Test body is not valid JSON: {exception.Message}"));
                    continue;
                }
                request.Content = new StringContent(test.Body, Encoding.UTF8, "application/json");
            }

            var response = await SendAsync(client, request);
            var passed = response.StatusCode.HasValue && (int)response.StatusCode.Value == test.ExpectedStatus;
            results.Add(new CheckResult(test.Name, passed, response.StatusCode is null ? null : (int)response.StatusCode,
                response.StatusCode is null ? response.Error! : $"expected {(int)test.ExpectedStatus}; received {(int)response.StatusCode.Value}"));
        }
    }

    private static async Task<int> RequestAsync(string[] args)
    {
        if (args.Length < 2) return Fail("Usage: request METHOD URL [--body JSON] [--token-env NAME] [--allow-write]");
        if (!Uri.TryCreate(args[1], UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return Fail("URL must be an absolute HTTP or HTTPS URL.");
        if (!TryParseMethod(args[0], out var method)) return Fail($"Unsupported HTTP method '{args[0]}'.");

        var isWrite = method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options;
        if (isWrite && !args.Contains("--allow-write", StringComparer.OrdinalIgnoreCase))
            return Fail("Request not sent: non-read methods require the explicit --allow-write flag.");

        var body = GetStringOption(args, "--body");
        if (body is not null)
        {
            try { using var _ = JsonDocument.Parse(body); }
            catch (JsonException exception) { return Fail($"--body must be valid JSON: {exception.Message}"); }
        }

        using var client = CreateClient(GetIntOption(args, "--timeout", 30, 1, 300));
        using var request = new HttpRequestMessage(method, uri);
        var tokenVariable = GetStringOption(args, "--token-env") ?? "LANDSCAPE_DEBUG_TOKEN";
        var token = Environment.GetEnvironmentVariable(tokenVariable);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var result = await SendAsync(client, request);
        Console.WriteLine(result.StatusCode.HasValue
            ? $"{(int)result.StatusCode.Value} {result.StatusCode.Value}  {uri}\n{result.Body}"
            : $"Request failed: {result.Error}");
        return result.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices ? 0 : 1;
    }

    private static async Task<int> ComposeAsync(string[] args)
    {
        var composeFile = GetStringOption(args, "--file") ?? FindComposeFile();
        var process = await RunProcessAsync("docker", ["compose", "-f", composeFile, "ps", "--services", "--filter", "status=running"]);
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"Docker Compose status failed: {process.Error.Trim()}");
            return 2;
        }

        var running = process.Output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = ExpectedComposeServices.Select(service => new CheckResult(
            $"Compose service {service}", running.Contains(service), null, running.Contains(service) ? "running" : "not running")).ToArray();
        PrintResults(results, args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        return results.All(result => result.Passed) ? 0 : 1;
    }

    private static async Task<CheckResult> CheckComposeAsync(string composeFile)
    {
        var process = await RunProcessAsync("docker", ["compose", "-f", composeFile, "ps", "--services", "--filter", "status=running"]);
        if (process.ExitCode != 0)
            return new CheckResult("Docker Compose status", false, null,
                string.IsNullOrWhiteSpace(process.Error) ? "Docker Compose command failed." : process.Error.Trim());

        var running = process.Output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = ExpectedComposeServices.Where(service => !running.Contains(service)).ToArray();
        return new CheckResult("Docker Compose services", missing.Length == 0, null,
            missing.Length == 0 ? "All API containers and the gateway are running." : $"Not running: {string.Join(", ", missing)}");
    }

    private static async Task<AuthResult> AuthenticateAsync(HttpClient client, Target target, string username, string password)
    {
        var body = JsonSerializer.Serialize(new LoginRequest(username, password), JsonOptions);
        var direct = await SendJsonAsync(client, HttpMethod.Post, Join(target.Identity.BaseUrl, "/api/v1/auth/login"), body);
        var gateway = await SendJsonAsync(client, HttpMethod.Post, Join(target.DefaultGateway, "/Auth/login"), body);
        var directStatus = direct.StatusCode;
        var gatewayStatus = gateway.StatusCode;
        var gatewayToken = TryReadAuthResponse(gateway.Body, out var token, out var role) ? token : null;
        if (gatewayToken is null && TryReadAuthResponse(direct.Body, out token, out var directRole))
        {
            gatewayToken = token;
            role ??= directRole;
        }
        return new AuthResult(directStatus, gatewayStatus, gatewayToken, role);
    }

    private static async Task<HttpStatusCode> AuthenticatedGetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await SendAsync(client, request);
        return response.StatusCode ?? 0;
    }

    private static bool TryReadAuthResponse(string body, out string? token, out string? role)
    {
        token = null;
        role = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (TryGetProperty(root, "accessToken", out var tokenElement)) token = tokenElement.GetString();
            if (TryGetProperty(root, "user", out var user) && TryGetProperty(user, "role", out var roleElement))
                role = roleElement.GetString();
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (JsonException) { return false; }
    }

    private static string? ReadJwtRole(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            foreach (var claim in document.RootElement.EnumerateObject())
            {
                if (claim.Name.Equals("role", StringComparison.OrdinalIgnoreCase) || claim.Name.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                    return claim.Value.ValueKind == JsonValueKind.Array ? claim.Value[0].GetString() : claim.Value.GetString();
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { }
        return null;
    }

    private static async Task<CheckResult> ProbeAsync(HttpClient client, string name, string url, HttpStatusCode[] expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await ProbeRequestAsync(client, name, request, expected);
    }

    private static async Task<CheckResult> ProbeRequestAsync(HttpClient client, string name, HttpRequestMessage request, HttpStatusCode[] expected)
    {
        var response = await SendAsync(client, request);
        if (response.StatusCode is null)
            return new CheckResult(name, false, null, response.Error ?? "Request failed.");
        return new CheckResult(name, expected.Contains(response.StatusCode.Value), (int)response.StatusCode.Value,
            $"{response.StatusCode.Value} in {response.ElapsedMilliseconds} ms");
    }

    private static async Task<HttpResult> SendJsonAsync(HttpClient client, HttpMethod method, string url, string json)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await SendAsync(client, request);
    }

    private static async Task<HttpResult> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var body = await response.Content.ReadAsStringAsync();
            timer.Stop();
            return new HttpResult(response.StatusCode, body, null, timer.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            timer.Stop();
            return new HttpResult(null, string.Empty, $"{exception.GetType().Name} after {timer.ElapsedMilliseconds} ms: {exception.Message}", timer.ElapsedMilliseconds);
        }
    }

    private static async Task<DiagnosticProfile> ReadProfileAsync(string file)
    {
        var content = await File.ReadAllTextAsync(file);
        return JsonSerializer.Deserialize<DiagnosticProfile>(content, JsonOptions)
            ?? throw new InvalidDataException($"Could not parse diagnostics profile: {file}");
    }

    private static string? GetUsername(TestUser user) =>
        string.IsNullOrWhiteSpace(user.UserNameEnv) ? user.UserName : Environment.GetEnvironmentVariable(user.UserNameEnv);

    private static IEnumerable<RouteProbe> LoadGetRoutes(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        if (!document.RootElement.TryGetProperty("Routes", out var routes))
            throw new InvalidDataException("Ocelot routes file does not contain a Routes array.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes.EnumerateArray())
        {
            if (!route.TryGetProperty("UpstreamPathTemplate", out var pathElement) ||
                !route.TryGetProperty("UpstreamHttpMethod", out var methodsElement) ||
                !methodsElement.EnumerateArray().Any(method => method.GetString()?.Equals("GET", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            var path = pathElement.GetString();
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) continue;
            var requiresAuthentication = route.TryGetProperty("AuthenticationOptions", out var auth) && auth.ValueKind == JsonValueKind.Object;
            yield return new RouteProbe(path, requiresAuthentication);
        }
    }

    private static string ReplaceRouteParameters(string route) =>
        Regex.Replace(route, "\\{[^}]+\\}", "00000000-0000-0000-0000-000000000001");

    private static Target GetTarget(string[] args)
    {
        var target = GetStringOption(args, "--target") ?? "outside";
        return target.ToLowerInvariant() switch
        {
            "outside" or "host" => Target.Outside,
            "docker" or "inside" or "container" => Target.InsideDocker,
            _ => throw new ArgumentException($"Unknown target '{target}'. Use outside or docker.")
        };
    }

    private static ServiceEndpoint[] GetEndpoints(Target target) =>
        (target.Inside ? DockerServices : LocalServices)
        .Select(service => new ServiceEndpoint(service.Name, $"http://{service.Host}:{service.Port}"))
        .ToArray();

    private static string FindRoutesFile()
    {
        var outputCopy = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
        if (File.Exists(outputCopy)) return outputCopy;
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "ApiGateways", "OcelotApiGateway", "ocelot.json");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(current.FullName, "src", "ApiGateways", "OcelotApiGateway", "ocelot.json");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return outputCopy;
    }

    private static string FindComposeFile()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docker-compose.yml");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(current.FullName, "src", "docker-compose.yml");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return "src/docker-compose.yml";
    }

    private static async Task<ProcessResult> RunProcessAsync(string executable, IEnumerable<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ProcessResult(127, string.Empty, exception.Message);
        }
    }

    private static HttpClient CreateClient(int timeoutSeconds) => new() { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
    private static string Join(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string? GetStringOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool TryParseMethod(string value, out HttpMethod method)
    {
        method = value.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "PATCH" => HttpMethod.Patch,
            "DELETE" => HttpMethod.Delete,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => null!
        };
        return method is not null;
    }

    private static int? ToStatusCode(HttpStatusCode? status) => status is null ? null : (int)status.Value;

    private static string FormatStatus(HttpStatusCode? status) =>
        status is null ? "no response" : $"{(int)status.Value} {status.Value}";

    private static int GetIntOption(string[] args, string name, int fallback, int minimum, int maximum)
    {
        var value = GetStringOption(args, name);
        if (value is null) return fallback;
        return int.TryParse(value, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed
            : throw new ArgumentException($"{name} must be an integer between {minimum} and {maximum}.");
    }

    private static string Prompt(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    private static string ReadSecret(string prompt)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("Set LANDSCAPE_TEST_PASSWORD or the --password-env variable when input is redirected.");
        Console.Write(prompt);
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0) value.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
        Console.WriteLine();
        return value.ToString();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static void PrintResults(IEnumerable<CheckResult> results, bool json)
    {
        var materialized = results.ToArray();
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(materialized, JsonOptions));
            return;
        }
        foreach (var result in materialized)
        {
            var code = result.StatusCode is null ? string.Empty : $" [{result.StatusCode}]";
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL"),-4} {result.Name}{code}: {result.Detail}");
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    private static void PrintUsage() => Console.WriteLine("""
        Landscape Studio diagnostics CLI

        check [--target outside|docker] [--gateway URL] [--routes-file PATH] [--token-env NAME] [--compose]
          Checks every API health and OpenAPI endpoint, then GET routes from Ocelot.
          'outside' uses localhost ports; 'docker' uses Compose DNS hostnames and ports.
          Set LANDSCAPE_DEBUG_TOKEN to exercise authenticated GET routes with a token.

        login [--target outside|docker] [--username NAME] [--password-env NAME] [--check-me]
          Tests login directly against Identity and through Ocelot, then optionally tests /me.
          Password is read from LANDSCAPE_TEST_PASSWORD or a hidden interactive prompt.
                    LANDSCAPE_TEST_USERNAME can supply the username for non-interactive runs.

        users --file users.json [--target outside|docker] [--allow-write]
          Tests multiple logins, expected roles, /me, and optional per-user HTTP cases.

        suite --file tests.json [--target outside|docker] [--allow-write] [--timeout SECONDS]
          Runs the configured multi-user authorization and API test matrix.

        request METHOD URL [--body JSON] [--token-env NAME] [--allow-write]
          Sends a custom request. Write methods require explicit --allow-write.

        compose [--file docker-compose.yml]
          Checks that all API and gateway services are running (requires Docker CLI).

        Examples:
          dotnet run -- check --target outside --compose
          dotnet run -- check --target docker
          dotnet run -- login --username admin --check-me
          dotnet run -- suite --file diagnostics.tests.json --target outside
          docker compose --profile diagnostics run --rm serverdiagnostics check --target docker
        """);

    private sealed record ServiceTarget(string Name, string Host, int Port)
    {
        public string BaseUrl => $"http://{Host}:{Port}";
    }

    private sealed record ServiceEndpoint(string Name, string BaseUrl);
    private sealed record RouteProbe(string Path, bool RequiresAuthentication);
    private sealed record Target(string Name, bool Inside, ServiceEndpoint Identity, string DefaultGateway)
    {
        public static Target Outside { get; } = new("outside", false,
            new ServiceEndpoint("Identity API", "http://localhost:8003"), "http://localhost:8010");
        public static Target InsideDocker { get; } = new("docker", true,
            new ServiceEndpoint("Identity API", "http://identity.api:8080"), "http://ocelotapigateway:8080");
    }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record AuthResult(HttpStatusCode? DirectStatus, HttpStatusCode? GatewayStatus, string? Token, string? Role);
    private sealed record HttpResult(HttpStatusCode? StatusCode, string Body, string? Error, long ElapsedMilliseconds);
    private sealed record ProcessResult(int ExitCode, string Output, string Error);
    private sealed record CheckResult(string Name, bool Passed, int? StatusCode, string Detail);

    private sealed class DiagnosticProfile
    {
        public List<TestUser> Users { get; init; } = [];
        public List<ApiTestCase> Tests { get; init; } = [];
    }

    private sealed class TestUser
    {
        public string Name { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string UserNameEnv { get; init; } = string.Empty;
        public string PasswordEnv { get; init; } = string.Empty;
        public string? ExpectedRole { get; init; }
    }

    private sealed class ApiTestCase
    {
        public string Name { get; init; } = string.Empty;
        public string Method { get; init; } = "GET";
        public string Path { get; init; } = "/";
        public int ExpectedStatus { get; init; } = 200;
        public string? User { get; init; }
        public string? Body { get; init; }
        public bool AllowWrite { get; init; }
    }
}
