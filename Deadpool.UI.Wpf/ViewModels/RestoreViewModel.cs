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
    private readonly IRestorePlannerService _planner;
    private readonly IRestorePlanValidatorService _validator;
    private readonly IRestoreSafetyGuard _safetyGuard;
    private readonly IRestoreOrchestratorService _orchestrator;
    private readonly ILogger<RestoreViewModel> _logger;

    private DateTime _targetTime;
    private RestorePlanViewModel? _plan;
    private RestoreValidationResult _validation = new();
    private bool _isValid;
    private bool _isConfirmed;
    private string _confirmationText = string.Empty;
    private string _statusMessage = "Select target restore time and generate plan.";
    private DateTime? _targetDate;
    private int _selectedHour;
    private int _selectedMinute;

    private RestorePlan? _currentPlan;

    public RestoreViewModel(
        IRestorePlannerService planner,
        IRestorePlanValidatorService validator,
        IRestoreSafetyGuard safetyGuard,
        IRestoreOrchestratorService orchestrator,
        IOptions<RestoreOrchestratorOptions> options,
        ILogger<RestoreViewModel> logger)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DatabaseName = options.Value.DatabaseName;
        if (string.IsNullOrWhiteSpace(DatabaseName))
            DatabaseName = "UNKNOWN";

        var now = DateTime.Now;
        _targetDate = now.Date;
        _selectedHour = now.Hour;
        _selectedMinute = now.Minute;
        _targetTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);

        Hours = new ObservableCollection<int>(Enumerable.Range(0, 24));
        Minutes = new ObservableCollection<int>(Enumerable.Range(0, 60));

        ValidationErrors = new ObservableCollection<string>();
        ValidationWarnings = new ObservableCollection<string>();

        GeneratePlanCommand = new AsyncCommand(GeneratePlanAsync);
        ExecuteRestoreCommand = new AsyncCommand(ExecuteRestoreAsync, CanExecuteRestore);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime TargetTime
    {
        get => _targetTime;
        private set => SetField(ref _targetTime, value);
    }

    public DateTime? TargetDate
    {
        get => _targetDate;
        set
        {
            if (SetField(ref _targetDate, value))
            {
                RebuildTargetTime();
            }
        }
    }

    public int SelectedHour
    {
        get => _selectedHour;
        set
        {
            if (SetField(ref _selectedHour, value))
            {
                RebuildTargetTime();
            }
        }
    }

    public int SelectedMinute
    {
        get => _selectedMinute;
        set
        {
            if (SetField(ref _selectedMinute, value))
            {
                RebuildTargetTime();
            }
        }
    }

    public ObservableCollection<int> Hours { get; }
    public ObservableCollection<int> Minutes { get; }

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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ObservableCollection<string> ValidationErrors { get; }
    public ObservableCollection<string> ValidationWarnings { get; }

    public AsyncCommand GeneratePlanCommand { get; }
    public AsyncCommand ExecuteRestoreCommand { get; }

    private async Task GeneratePlanAsync()
    {
        try
        {
            StatusMessage = "Generating restore plan...";
            var targetUtc = TargetTime.ToUniversalTime();

            var plan = await _planner.BuildRestorePlanAsync(DatabaseName, targetUtc);
            _currentPlan = plan;
            Plan = BuildPlanViewModel(plan);

            var validation = _validator.Validate(plan);
            Validation = validation;
            IsValid = validation.IsValid;

            SyncValidationCollections(validation);

            StatusMessage = validation.IsValid
                ? "Plan generated and validated. Ready for confirmation."
                : "Plan generated, but validation failed. Resolve errors before execution.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore plan generation failed for {DatabaseName}.", DatabaseName);
            IsValid = false;
            Validation = new RestoreValidationResult();
            ValidationErrors.Clear();
            ValidationWarnings.Clear();
            ValidationErrors.Add(ex.Message);
            StatusMessage = "Failed to generate restore plan.";
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private async Task ExecuteRestoreAsync()
    {
        if (_currentPlan == null || !IsValid)
        {
            StatusMessage = "Generate and validate a restore plan before execution.";
            return;
        }

        try
        {
            var context = new RestoreConfirmationContext
            {
                DatabaseName = DatabaseName,
                Confirmed = IsConfirmed,
                ConfirmationText = ConfirmationText,
                RequireTextMatch = true
            };

            _safetyGuard.EnsureConfirmed(context);

            StatusMessage = "Executing restore...";
            await _orchestrator.ExecuteRestore(TargetTime.ToUniversalTime(), context);
            StatusMessage = "Restore execution completed successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore execution failed for {DatabaseName}.", DatabaseName);
            StatusMessage = "Restore execution failed: " + ex.Message;
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    private bool CanExecuteRestore()
    {
        if (!IsValid || _currentPlan == null)
            return false;

        if (!IsConfirmed)
            return false;

        return string.Equals(ConfirmationText, DatabaseName, StringComparison.Ordinal);
    }

    private void RebuildTargetTime()
    {
        var date = TargetDate ?? DateTime.Now.Date;
        TargetTime = new DateTime(
            date.Year,
            date.Month,
            date.Day,
            SelectedHour,
            SelectedMinute,
            0,
            DateTimeKind.Local);
    }

    private RestorePlanViewModel BuildPlanViewModel(RestorePlan plan)
    {
        var steps = new List<RestoreStepViewModel>();

        if (plan.FullBackup != null)
        {
            steps.Add(ToStep(plan.FullBackup, isStopAt: false));
        }

        if (plan.DifferentialBackup != null)
        {
            steps.Add(ToStep(plan.DifferentialBackup, isStopAt: false));
        }

        for (var i = 0; i < plan.LogBackups.Count; i++)
        {
            var isLast = i == plan.LogBackups.Count - 1;
            steps.Add(ToStep(plan.LogBackups[i], isStopAt: isLast));
        }

        return new RestorePlanViewModel(steps, plan.RequestedRestorePoint);
    }

    private static RestoreStepViewModel ToStep(BackupJob job, bool isStopAt)
    {
        return new RestoreStepViewModel(
            type: job.BackupType switch
            {
                BackupType.Full => "FULL",
                BackupType.Differential => "DIFF",
                BackupType.TransactionLog => "LOG",
                _ => job.BackupType.ToString().ToUpperInvariant()
            },
            fileName: Path.GetFileName(job.BackupFilePath),
            completedAt: job.EndTime ?? job.StartTime,
            isStopAt: isStopAt);
    }

    private void SyncValidationCollections(RestoreValidationResult validation)
    {
        ValidationErrors.Clear();
        foreach (var error in validation.Errors)
        {
            ValidationErrors.Add(error);
        }

        ValidationWarnings.Clear();
        foreach (var warning in validation.Warnings)
        {
            ValidationWarnings.Add(warning);
        }
    }

    private void RaiseCommandStates()
    {
        GeneratePlanCommand.RaiseCanExecuteChanged();
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
    public RestoreStepViewModel(string type, string fileName, DateTime completedAt, bool isStopAt)
    {
        Type = type;
        FileName = fileName;
        CompletedAt = completedAt;
        IsStopAt = isStopAt;
    }

    public string Type { get; }
    public string FileName { get; }
    public DateTime CompletedAt { get; }
    public bool IsStopAt { get; }
}
