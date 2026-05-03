using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Deadpool.Core.Configuration;
using Deadpool.Core.Domain.Entities;
using Deadpool.Core.Domain.Enums;
using Deadpool.Core.Domain.ValueObjects;
using Deadpool.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deadpool.UI.Wpf.ViewModels;

public sealed class RestoreViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<BackupType> RestorePointTypes = new()
    {
        BackupType.Full,
        BackupType.Differential,
        BackupType.TransactionLog
    };

    private readonly IRestorePlannerService _planner;
    private readonly IRestorePlanValidatorService _validator;
    private readonly IRestoreSafetyGuard _safetyGuard;
    private readonly IRestoreOrchestratorService _orchestrator;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly ILogger<RestoreViewModel> _logger;
    private readonly Dictionary<string, (RestorePlan Plan, RestoreValidationResult Validation)> _planCache = new();

    private RestorePointViewModel? _selectedRestorePoint;
    private RestorePlanViewModel? _plan;
    private RestoreValidationResult _validation = new();
    private string _restorePlanSummary = "Select a restore point.";
    private string _baseBackupSummary = "-";
    private string _logChainSummary = "-";
    private string _stopAtSummary = "-";
    private bool _showPlanDetails;
    private bool _isValid;
    private bool _isConfirmed;
    private bool _requireTextMatch;
    private string _confirmationText = string.Empty;
    private string _validationMessage = "Select a restore point to evaluate restore readiness.";

    private RestorePlan? _currentPlan;

    public RestoreViewModel(
        IRestorePlannerService planner,
        IRestorePlanValidatorService validator,
        IRestoreSafetyGuard safetyGuard,
        IRestoreOrchestratorService orchestrator,
        IBackupJobRepository backupJobRepository,
        IOptions<RestoreOrchestratorOptions> options,
        ILogger<RestoreViewModel> logger)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DatabaseName = options.Value.DatabaseName;
        if (string.IsNullOrWhiteSpace(DatabaseName))
            DatabaseName = "UNKNOWN";

        _requireTextMatch = options.Value.RequireTextMatch;

        RestorePoints = new ObservableCollection<RestorePointViewModel>();
        RefreshRestorePointsCommand = new AsyncCommand(LoadRestorePointsAsync);
        ExecuteRestoreCommand = new AsyncCommand(ExecuteRestoreAsync, CanExecuteRestore);

        _ = LoadRestorePointsAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RestorePointViewModel> RestorePoints { get; }

    public RestorePointViewModel? SelectedRestorePoint
    {
        get => _selectedRestorePoint;
        set
        {
            if (SetField(ref _selectedRestorePoint, value))
            {
                _ = OnSelectedRestorePointChangedAsync();
            }
        }
    }

    public RestorePlanViewModel? Plan
    {
        get => _plan;
        private set => SetField(ref _plan, value);
    }

    public RestoreValidationResult Validation
    {
        get => _validation;
        private set => SetField(ref _validation, value);
    }

    public string RestorePlanSummary
    {
        get => _restorePlanSummary;
        private set => SetField(ref _restorePlanSummary, value);
    }

    public string BaseBackupSummary
    {
        get => _baseBackupSummary;
        private set => SetField(ref _baseBackupSummary, value);
    }

    public string LogChainSummary
    {
        get => _logChainSummary;
        private set => SetField(ref _logChainSummary, value);
    }

    public string StopAtSummary
    {
        get => _stopAtSummary;
        private set => SetField(ref _stopAtSummary, value);
    }

    public bool ShowPlanDetails
    {
        get => _showPlanDetails;
        set => SetField(ref _showPlanDetails, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetField(ref _isValid, value);
    }

    public bool IsConfirmed
    {
        get => _isConfirmed;
        set
        {
            if (SetField(ref _isConfirmed, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool RequireTextMatch
    {
        get => _requireTextMatch;
        set
        {
            if (SetField(ref _requireTextMatch, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ConfirmationText
    {
        get => _confirmationText;
        set
        {
            if (SetField(ref _confirmationText, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string DatabaseName { get; }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetField(ref _validationMessage, value);
    }

    public AsyncCommand RefreshRestorePointsCommand { get; }
    public AsyncCommand ExecuteRestoreCommand { get; }

    private async Task LoadRestorePointsAsync()
    {
        try
        {
            ValidationMessage = "Loading available restore points...";
            _planCache.Clear();

            var completedBackups = (await _backupJobRepository.GetBackupsByDatabaseAsync(DatabaseName))
                .Where(j => j.Status == BackupStatus.Completed)
                .Where(j => RestorePointTypes.Contains(j.BackupType))
                .Select(j => new
                {
                    Backup = j,
                    Time = GetRestorePointLocalTime(j)
                })
                .Where(x => x.Time.HasValue)
                .OrderBy(x => x.Time)
                .GroupBy(x => new { x.Backup.BackupType, Ticks = x.Time!.Value.Ticks })
                .Select(g => g.First())
                .ToList();

            RestorePoints.Clear();

            foreach (var candidate in completedBackups)
            {
                var targetTime = candidate.Time!.Value;
                var cached = await GetOrCreatePlanAndValidationAsync(targetTime, candidate.Backup.BackupType);
                var isSelectable = cached.Plan.IsValid && cached.Validation.IsValid;

                RestorePoints.Add(new RestorePointViewModel(
                    time: targetTime,
                    type: candidate.Backup.BackupType,
                    isSelectable: isSelectable));
            }

            var nextSelection = RestorePoints.LastOrDefault(r => r.IsSelectable)
                ?? RestorePoints.LastOrDefault();

            SelectedRestorePoint = nextSelection;

            if (nextSelection == null)
            {
                ResetCurrentPlanState("No completed backups available to build restore points.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load restore points for {DatabaseName}.", DatabaseName);
            RestorePoints.Clear();
            SelectedRestorePoint = null;
            ResetCurrentPlanState("Failed to load restore points.");
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private async Task OnSelectedRestorePointChangedAsync()
    {
        if (SelectedRestorePoint == null)
        {
            ResetCurrentPlanState("Select a restore point to evaluate restore readiness.");
            RaiseCommandStates();
            return;
        }

        try
        {
            var selected = SelectedRestorePoint;
            var cached = await GetOrCreatePlanAndValidationAsync(selected.Time, selected.Type);

            var plan = cached.Plan;
            var validation = cached.Validation;
            _currentPlan = plan;
            Plan = BuildPlanViewModel(plan);
            Validation = validation;
            IsValid = plan.IsValid && validation.IsValid;

            UpdatePlanSummary(plan, selected.Time);
            ValidationMessage = BuildValidationMessage(plan, validation);

            if (!selected.IsSelectable && IsValid)
            {
                selected.IsSelectable = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate restore point for {DatabaseName}.", DatabaseName);
            ResetCurrentPlanState("Failed to evaluate selected restore point.");
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private async Task<(RestorePlan Plan, RestoreValidationResult Validation)> GetOrCreatePlanAndValidationAsync(
        DateTime targetTime,
        BackupType type)
    {
        var key = BuildPlanCacheKey(targetTime, type);
        if (_planCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var plan = await _planner.BuildRestorePlanAsync(DatabaseName, targetTime);
        var validation = _validator.Validate(plan);
        var result = (plan, validation);
        _planCache[key] = result;
        return result;
    }

    private static string BuildPlanCacheKey(DateTime targetTime, BackupType type)
        => $"{targetTime.Ticks}:{(int)type}";

    private static DateTime? GetRestorePointLocalTime(BackupJob backup)
    {
        if (backup.EndTime.HasValue)
            return backup.EndTime.Value.ToLocalTime();

        if (backup.ExecutionStartTime.HasValue)
            return backup.ExecutionStartTime.Value.ToLocalTime();

        return null;
    }

    private void UpdatePlanSummary(RestorePlan plan, DateTime selectedPointLocal)
    {
        if (!plan.IsValid)
        {
            BaseBackupSummary = "-";
            LogChainSummary = "-";
            StopAtSummary = selectedPointLocal.ToString("HH:mm");
            RestorePlanSummary = "No valid plan for selected restore point.";
            return;
        }

        BaseBackupSummary = plan.DifferentialBackup != null ? "FULL + DIFF" : "FULL";
        LogChainSummary = $"{plan.LogBackups.Count} file(s)";
        StopAtSummary = selectedPointLocal.ToString("HH:mm");
        RestorePlanSummary = $"Base: {BaseBackupSummary} | Logs: {LogChainSummary} | StopAt: {StopAtSummary}";
    }

    private static string BuildValidationMessage(RestorePlan plan, RestoreValidationResult validation)
    {
        if (plan.IsValid && validation.IsValid)
            return "Ready to execute restore.";

        if (validation.Errors.Count > 0)
            return validation.Errors[0];

        if (!string.IsNullOrWhiteSpace(plan.FailureReason))
            return plan.FailureReason;

        if (validation.Warnings.Count > 0)
            return validation.Warnings[0];

        return "Restore plan is invalid for the selected restore point.";
    }

    private void ResetCurrentPlanState(string validationMessage)
    {
        _currentPlan = null;
        Plan = null;
        Validation = new RestoreValidationResult();
        IsValid = false;
        BaseBackupSummary = "-";
        LogChainSummary = "-";
        StopAtSummary = "-";
        RestorePlanSummary = "Select a restore point.";
        ValidationMessage = validationMessage;
    }

    private async Task ExecuteRestoreAsync()
    {
        if (_currentPlan == null || !IsValid || SelectedRestorePoint == null)
        {
            ValidationMessage = "Select a valid restore point before execution.";
            return;
        }

        try
        {
            var context = new RestoreConfirmationContext
            {
                DatabaseName = DatabaseName,
                Confirmed = IsConfirmed,
                ConfirmationText = ConfirmationText,
                RequireTextMatch = RequireTextMatch
            };

            _safetyGuard.EnsureConfirmed(context);

            ValidationMessage = "Executing restore...";
            await _orchestrator.ExecuteRestore(SelectedRestorePoint.Time, context);
            ValidationMessage = "Restore execution completed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore execution failed for {DatabaseName}.", DatabaseName);
            ValidationMessage = "Restore execution failed: " + ex.Message;
        }
        finally
        {
            await LoadRestorePointsAsync();
            RaiseCommandStates();
        }
    }

    private bool CanExecuteRestore()
    {
        if (!IsValid || _currentPlan == null || SelectedRestorePoint == null)
            return false;

        if (!IsConfirmed)
            return false;

        if (!RequireTextMatch)
            return true;

        return string.Equals(ConfirmationText, DatabaseName, StringComparison.Ordinal);
    }
    private RestorePlanViewModel BuildPlanViewModel(RestorePlan plan)
    {
        if (!plan.IsValid)
            return new RestorePlanViewModel(Array.Empty<RestoreStepViewModel>(), plan.RequestedRestorePoint);

        var steps = new List<RestoreStepViewModel>();
        var hasLogs = plan.LogBackups.Count > 0;
        var stopAtAssigned = false;

        if (plan.FullBackup != null)
        {
            var fullIsStopAt = !hasLogs && plan.DifferentialBackup == null;
            steps.Add(ToStep(plan.FullBackup, isStopAt: fullIsStopAt, order: steps.Count + 1));
            stopAtAssigned = fullIsStopAt;
        }

        if (plan.DifferentialBackup != null)
        {
            var diffIsStopAt = !hasLogs;
            steps.Add(ToStep(plan.DifferentialBackup, isStopAt: diffIsStopAt, order: steps.Count + 1));
            stopAtAssigned = stopAtAssigned || diffIsStopAt;
        }

        for (var i = 0; i < plan.LogBackups.Count; i++)
        {
            var isLast = i == plan.LogBackups.Count - 1;
            steps.Add(ToStep(plan.LogBackups[i], isStopAt: isLast, order: steps.Count + 1));
            if (isLast)
            {
                stopAtAssigned = true;
            }
        }

        // Defensive fallback: ensure STOPAT is always visible on at least one step.
        if (!stopAtAssigned && steps.Count > 0)
        {
            var last = steps[^1];
            steps[^1] = new RestoreStepViewModel(
                stepType: last.StepType,
                displayName: last.DisplayName,
                isStopAt: true,
                order: last.Order,
                isLast: true);
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var isLast = i == steps.Count - 1;
            if (steps[i].IsLast != isLast)
            {
                var s = steps[i];
                steps[i] = new RestoreStepViewModel(
                    stepType: s.StepType,
                    displayName: s.DisplayName,
                    isStopAt: s.IsStopAt,
                    order: s.Order,
                    isLast: isLast);
            }
        }

        return new RestorePlanViewModel(steps, plan.RequestedRestorePoint);
    }

    private static RestoreStepViewModel ToStep(BackupJob job, bool isStopAt, int order)
    {
        return new RestoreStepViewModel(
            stepType: job.BackupType switch
            {
                BackupType.Full => "FULL",
                BackupType.Differential => "DIFF",
                BackupType.TransactionLog => "LOG",
                _ => job.BackupType.ToString().ToUpperInvariant()
            },
            displayName: Path.GetFileName(job.BackupFilePath),
            isStopAt: isStopAt,
            order: order,
            isLast: false);
    }

    private void RaiseCommandStates()
    {
        RefreshRestorePointsCommand.RaiseCanExecuteChanged();
        ExecuteRestoreCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class RestorePlanViewModel
{
    public RestorePlanViewModel(IReadOnlyList<RestoreStepViewModel> steps, DateTime stopAt)
    {
        Steps = steps;
        StopAt = stopAt;
    }

    public IReadOnlyList<RestoreStepViewModel> Steps { get; }
    public DateTime StopAt { get; }
}

public sealed class RestoreStepViewModel
{
    public RestoreStepViewModel(string stepType, string displayName, bool isStopAt, int order, bool isLast)
    {
        StepType = stepType;
        DisplayName = displayName;
        IsStopAt = isStopAt;
        Order = order;
        IsLast = isLast;
    }

    public string StepType { get; }
    public string DisplayName { get; }
    public bool IsStopAt { get; }
    public int Order { get; }
    public bool IsLast { get; }
}

public sealed class RestorePointViewModel : INotifyPropertyChanged
{
    private bool _isSelectable;

    public RestorePointViewModel(DateTime time, BackupType type, bool isSelectable)
    {
        Time = time;
        Type = type;
        _isSelectable = isSelectable;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime Time { get; }
    public BackupType Type { get; }

    public bool IsSelectable
    {
        get => _isSelectable;
        set
        {
            if (_isSelectable == value)
                return;

            _isSelectable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectable)));
        }
    }

    public string TimeText => Time.ToString("HH:mm");

    public string TypeText => Type switch
    {
        BackupType.Full => "FULL",
        BackupType.Differential => "DIFF",
        BackupType.TransactionLog => "LOG",
        _ => Type.ToString().ToUpperInvariant()
    };
}
