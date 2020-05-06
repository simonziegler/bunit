using System;

namespace Bunit
{
    /// <summary>
    /// Represents an exception thrown when the state predicate does not pass or if it throws itself.
    /// </summary>
    public class WaitForStateFailedException : Exception
    {
        internal const string TIMEOUT_BEFORE_PASS = "The state predicate did not pass before the timeout period passed.";
        internal const string EXCEPTION_IN_PREDICATE = "The state predicate throw an unhandled exception.";

		/// <summary>
		/// Creates an instance of the <see cref="WaitForStateFailedException"/>.
		/// </summary>
        public WaitForStateFailedException(string errorMessage, Exception? innerException = null) : base(errorMessage, innerException)
        {
        }
    }
}
