using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YAKL.Core;

namespace YAKL.LauncherWPF.ViewModels
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        public System.Collections.ObjectModel.ObservableCollection<LocalMod> Mods { get; set; }

        //public System.Collections.ObjectModel.ObservableCollection<TaskWithDescription> BackgroundTasks { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected YAKLService _yaklService = new YAKLService();

        public MainWindowVM()
        {
            LaunchGame = new LaunchGameCommand();
            UpdateMod = new UpdateModCommand(_yaklService);
        }

        public void Initialize()
        {
            var ret = _yaklService.LoadLocalMods().ConfigureAwait(false).GetAwaiter().GetResult();
            Mods = new System.Collections.ObjectModel.ObservableCollection<LocalMod>(ret);
        }

        protected void OnNotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected LocalMod _SelectedMod;

        public LocalMod SelectedMod
        {
            get
            {
                return _SelectedMod;
            }
            set
            {
                _SelectedMod = value;
                OnNotifyPropertyChanged(nameof(SelectedMod));
            }
        }

        public ICommand LaunchGame { get; set; }

        public ICommand UpdateMod { get; set; }
    }

    public class UpdateModCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private YAKLService _service;

        public UpdateModCommand(YAKLService service)
        { _service = service; }

        public bool CanExecute(object? parameter)
        {
            if (parameter == null) return false;
            LocalMod localMod = parameter as LocalMod;

            return localMod.NeedUpdate ?? false;
        }

        public async void Execute(object? parameter)
        {
            LocalMod localMod = parameter as LocalMod;

            await _service.UpdateMod(localMod);
        }
    }

    public class LaunchGameCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.RedirectStandardOutput = false;
            //TODO detect keeper rl paths
            var kp = "c:\\Program Files (x86)\\Steam\\steamapps\\common\\KeeperRL\\keeper.exe";
            p.StartInfo.FileName = kp;
            p.StartInfo.WorkingDirectory = "c:\\Program Files (x86)\\Steam\\steamapps\\common\\KeeperRL";
            p.Start();
        }
    }

    public class TaskWithDescription
    {
        public Task Task { get; set; }

        public string Description { get; set; }
    }
}