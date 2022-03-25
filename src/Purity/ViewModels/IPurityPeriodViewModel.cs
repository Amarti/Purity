using System;


namespace Purity.ViewModels
{
	/// <summary>
	/// Lean interface for viewmodels interoperability
	/// </summary>
	public interface IPurityPeriodViewModel
	{
		/// <summary>
		/// Refresh sub events
		/// </summary>
		void Refresh();


		DateTime SelectedBeginDate { get; }
	}
}
