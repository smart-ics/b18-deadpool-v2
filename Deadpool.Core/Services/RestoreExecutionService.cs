using System.Data;
using Deadpool.Core.Configuration;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deadpool.Core.Services;

/// <summary>
/// Executes restore SQL commands sequentially with fail-fast semantics.
/// </summary>
public class RestoreExecutionService : IRestoreExecutionService
{
    private readonly IRestoreScriptBuilderService _scriptBuilder;
    private readonly IOptions<RestoreExecutionOptions> _options;
    private readonly ILogger<RestoreExecutionService> _logger;

    public RestoreExecutionService(
        IRestoreScriptBuilderService scriptBuilder,
        IOptions<RestoreExecutionOptions> options,
        ILogger<RestoreExecutionService> logger)
    {
        _scriptBuilder = scriptBuilder ?? throw new ArgumentNullException(nameof(scriptBuilder));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RestoreExecutionResult> ExecuteAsync(
        RestorePlan plan,
        bool allowOverwrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!allowOverwrite)
        {
            throw new InvalidOperationException("Restore execution requires explicit overwrite consent. Set allowOverwrite=true to proceed.");
        }

        var result = new RestoreExecutionResult();

        try
        {
            var script = _scriptBuilder.Build(plan);

            var connectionString = _options.Value.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Restore execution requires a configured SQL connection string.");

            await using var connection = new SqlConnection(connectionString);
            await OpenConnectionAsync(connection, cancellationToken);

            var databaseExists = await DatabaseExistsAsync(connection, plan.DatabaseName, cancellationToken);

            _logger.LogInformation("Restore target database {DatabaseName} exists: {DatabaseExists}", plan.DatabaseName, databaseExists);

            var stepIndex = 1;
            foreach (var commandText in script.Commands)
            {
                var commandContext = GetCommandContext(commandText);
                var commandHash = ComputeCommandHash(commandText);
                var sanitizedCommand = SanitizeCommandForLog(commandText);

                var step = new RestoreStepLog
                {
                    Command = commandText
                };

                try
                {
                    _logger.LogInformation(
                        "Executing step {StepIndex} ({CommandContext}) [hash:{CommandHash}]: {SanitizedCommand}",
                        stepIndex,
                        commandContext,
                        commandHash,
                        sanitizedCommand);

                    await ExecuteCommandAsync(connection, commandText, cancellationToken);
                    step.Success = true;
                    result.Steps.Add(step);
                    _logger.LogInformation("Step {StepIndex} succeeded ({CommandContext}) [hash:{CommandHash}]", stepIndex, commandContext, commandHash);
                    stepIndex++;
                }
                catch (SqlException ex)
                {
                    var mappedError = MapSqlExceptionMessage(ex);
                    step.Success = false;
                    step.Error = mappedError;
                    result.Steps.Add(step);

                    result.Success = false;
                    result.ErrorMessage = mappedError;
                    _logger.LogError(ex, "Step {StepIndex} FAILED ({CommandContext}) [hash:{CommandHash}]: {Error}", stepIndex, commandContext, commandHash, mappedError);
                    return result;
                }
                catch (TimeoutException ex)
                {
                    step.Success = false;
                    step.Error = ex.Message;
                    result.Steps.Add(step);

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Step {StepIndex} FAILED ({CommandContext}) [hash:{CommandHash}]: {Error}", stepIndex, commandContext, commandHash, ex.Message);
                    return result;
                }
                catch (Exception ex)
                {
                    step.Success = false;
                    step.Error = ex.Message;
                    result.Steps.Add(step);

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Step {StepIndex} FAILED ({CommandContext}) [hash:{CommandHash}]: {Error}", stepIndex, commandContext, commandHash, ex.Message);
                    return result;
                }
            }

            result.Success = true;
            return result;
        }
        catch (SqlException ex)
        {
            result.Success = false;
            var mappedError = MapSqlExceptionMessage(ex);
            result.ErrorMessage = mappedError;
            _logger.LogError(ex, "Restore execution failed with SQL error before command completion. {Error}", mappedError);
            return result;
        }
        catch (TimeoutException ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Restore execution failed with timeout before command completion.");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Restore execution failed.");
            return result;
        }
    }

    protected virtual async Task<bool> DatabaseExistsAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.databases WHERE name = @databaseName) THEN 1 ELSE 0 END;";
        command.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128) { Value = databaseName });

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is int intValue)
            return intValue == 1;

        if (scalar is long longValue)
            return longValue == 1L;

        return false;
    }

    protected virtual async Task ExecuteCommandAsync(
        SqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = commandText;

        if (_options.Value.CommandTimeoutSeconds > 0)
        {
            command.CommandTimeout = _options.Value.CommandTimeoutSeconds;
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected virtual Task OpenConnectionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        return connection.OpenAsync(cancellationToken);
    }

    private static string MapSqlExceptionMessage(SqlException ex)
    {
        var isTimeout = ex.Number == -2 || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        if (isTimeout)
            return "SQL Timeout: " + ex.Message;

        return ex.Message;
    }

    private static string GetCommandContext(string commandText)
    {
        if (commandText.Contains("RESTORE LOG", StringComparison.OrdinalIgnoreCase))
            return "LOG";

        if (commandText.Contains("RESTORE DATABASE", StringComparison.OrdinalIgnoreCase)
            && commandText.Contains("DIFFERENTIAL", StringComparison.OrdinalIgnoreCase))
            return "DIFF";

        if (commandText.Contains("RESTORE DATABASE", StringComparison.OrdinalIgnoreCase))
            return "FULL";

        return "UNKNOWN";
    }

    private static string SanitizeCommandForLog(string commandText)
    {
        return commandText
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string ComputeCommandHash(string commandText)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(commandText ?? string.Empty);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
