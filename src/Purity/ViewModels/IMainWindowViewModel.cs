using System.Collections.Generic;


namespace Purity.ViewModels
{
	/// <summary>
	/// Lean interface for viewmodels interoperability
	/// </summary>
	public interface IMainWindowViewModel
	{
		/// <summary>
		/// Accept new period into the Data and calculate its events
		/// </summary>
		/// <param name="period">Period to be accepted</param>
		void AcceptPeriod(PurityPeriod period);
		/// <summary>
		/// Remove period from the Data and recalculater Data integrity
		/// </summary>
		/// <param name="period"></param>
		void RemovePeriod(PurityPeriod period);


		List<PurityPeriod> Data { get; }
	}
}
