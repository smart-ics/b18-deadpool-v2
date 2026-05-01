using System.Collections.ObjectModel;

namespace Deadpool.UI.Wpf.ViewModels;

public sealed class DashboardViewModel
{
    public ObservableCollection<JobRow> RecentJobs { get; } = new();

    public static DashboardViewModel CreateDesignSample()
    {
        var vm = new DashboardViewModel();

        vm.RecentJobs.Add(new JobRow("FULL", "10:25", "SUCCESS"));
        vm.RecentJobs.Add(new JobRow("DIFF", "10:26", "SUCCESS"));
        vm.RecentJobs.Add(new JobRow("LOG", "10:26", "SUCCESS"));
        vm.RecentJobs.Add(new JobRow("LOG", "10:27", "FAILED"));
        vm.RecentJobs.Add(new JobRow("DIFF", "10:30", "PENDING"));

        return vm;
    }
}

public sealed record JobRow(string Type, string Time, string Status);
