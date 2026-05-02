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
            await connection.OpenAsync(cancellationToken);

            var databaseExists = await DatabaseExistsAsync(connection, plan.DatabaseName, cancellationToken);

            _logger.LogInformation("Restore target database {DatabaseName} exists: {DatabaseExists}", plan.DatabaseName, databaseExists);

            foreach (var commandText in script.Commands)
            {
                var step = new RestoreStepLog
                {
                    Command = commandText
                };

                try
                {
                    await ExecuteCommandAsync(connection, commandText, cancellationToken);
                    step.Success = true;
                    result.Steps.Add(step);
                    _logger.LogInformation("Restore command executed successfully.");
                }
                catch (SqlException ex)
                {
                    step.Success = false;
                    step.Error = ex.Message;
                    result.Steps.Add(step);

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Restore command failed with SQL error. Execution stopped.");
                    return result;
                }
                catch (TimeoutException ex)
                {
                    step.Success = false;
                    step.Error = ex.Message;
                    result.Steps.Add(step);

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Restore command failed with timeout. Execution stopped.");
                    return result;
                }
                catch (Exception ex)
                {
                    step.Success = false;
                    step.Error = ex.Message;
                    result.Steps.Add(step);

                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Restore command failed. Execution stopped.");
                    return result;
                }
            }

            result.Success = true;
            return result;
        }
        catch (SqlException ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Restore execution failed with SQL error before command completion.");
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
}
