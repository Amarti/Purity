using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using Purity.ViewModels;
using ReactiveUI;


namespace Purity.Avalonia.ViewModels
{
	public class PurityPeriodViewModel : ViewModelBase, IPurityPeriodViewModel, IDisposable
	{
		public PurityPeriodViewModel(PurityPeriod period, IMainWindowViewModel ownerVM, Window ownerWindow)
		{
			_period = period;
			_ownerVM = ownerVM;
			_ownerWindow = ownerWindow;
			_impl = new PurityPeriodViewModelImpl(_period, _ownerVM);
			UpdatePeriodLength();

			SelectedBeginDateHalfDayCommand = ReactiveCommand.Create(SelectedBeginDateHalfDay);
			SelectedEndDateHalfDayCommand = ReactiveCommand.Create(SelectedEndDateHalfDay);
			AcceptPeriodCommand = ReactiveCommand.Create(AcceptPeriod);
			RemovePeriodCommand = ReactiveCommand.Create(RemovePeriod);
		}


		public void Dispose()
		{
			_impl.Dispose();
		}


		private void UpdatePeriodLength()
		{
			_impl.UpdatePeriodLength();
			this.RaisePropertyChanged(nameof(SkipPeriodLength));
		}

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(SkipPeriodLength));
			this.RaisePropertyChanged(nameof(IsClosed));
			this.RaisePropertyChanged(nameof(IsLast));
			_impl.Refresh();
		}

		public ICommand SelectedBeginDateHalfDayCommand { get; }
		internal void SelectedBeginDateHalfDay()
		{
			SelectedBeginDateIsAfterDusk = !SelectedBeginDateIsAfterDusk;
		}
		public ICommand SelectedEndDateHalfDayCommand { get; }
		internal void SelectedEndDateHalfDay()
		{
			SelectedEndDateIsAfterDusk = !SelectedEndDateIsAfterDusk;
		}
		public ICommand AcceptPeriodCommand { get; }
		internal void AcceptPeriod()
		{
			_ownerVM.AcceptPeriod(_period);
			UpdatePeriodLength();
			Refresh();
		}
		public ICommand RemovePeriodCommand { get; }
		internal async void RemovePeriod()
		{
			if (!IsClosed
			||	 await MessageBoxManager.GetMessageBoxStandardWindow(LogEntry.ProductName, "Are you sure you want to remove this period?",
																	 ButtonEnum.YesNo, Icon.Warning, WindowStartupLocation.CenterOwner, Style.RoundButtons)
										.ShowDialog(_ownerWindow) == ButtonResult.Yes)
				_ownerVM.RemovePeriod(_period);
		}


		public DateTimeOffset SelectedBeginDateOffset
		{
			get => new(_impl.SelectedBeginDate, TimeSpan.Zero);
			set
			{
				_impl.SelectedBeginDate = new DateTime(value.Ticks, DateTimeKind.Utc);
				UpdatePeriodLength();
				this.RaisePropertyChanged(nameof(SelectedBeginDateOffset));
			}
		}
		public DateTime SelectedBeginDate
		{
			get => _impl.SelectedBeginDate;
		}
		public bool SelectedBeginDateIsAfterDusk
		{
			get => _impl.SelectedBeginDateIsAfterDusk;
			set
			{
				_impl.SelectedBeginDateIsAfterDusk = value;
				UpdatePeriodLength();
				this.RaisePropertyChanged(nameof(SelectedBeginDateIsAfterDusk));
			}
		}
		public DateTimeOffset SelectedEndDateOffset
		{
			get => new(_impl.SelectedEndDate, TimeSpan.Zero);
			set
			{
				_impl.SelectedEndDate = new DateTime(value.Ticks, DateTimeKind.Utc);
				this.RaisePropertyChanged(nameof(SelectedEndDateOffset));
			}
		}
		public bool SelectedEndDateIsAfterDusk
		{
			get => _impl.SelectedEndDateIsAfterDusk;
			set
			{
				_impl.SelectedEndDateIsAfterDusk = value;
				this.RaisePropertyChanged(nameof(SelectedEndDateIsAfterDusk));
			}
		}
		public bool SkipStreak
		{
			get => _impl.SkipStreak;
			set
			{
				_impl.SkipStreak = value;
				this.RaisePropertyChanged(nameof(SkipStreak));
			}
		}
		public string SkipPeriodLength => _impl.SkipPeriodLength;
		public bool IsClosed => _impl.IsClosed;
		public bool IsLast => _impl.IsLast;
		public ObservableCollection<PurityEvent> SubEvents => _impl.SubEvents;

		private readonly PurityPeriod _period;
		private readonly IMainWindowViewModel _ownerVM;
		private readonly Window _ownerWindow;
		private readonly PurityPeriodViewModelImpl _impl;
	}
}
