namespace ServiceControl.UnitTests.Operations
{
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryOperationProgressionCalculatorTests
    {
        [Test]
        public void Completed_should_be_100_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 2000, 0, RetryState.Completed);
            Assert.AreEqual(1.0, calculated);
        }

        [Test]
        public void Waiting_should_be_0_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(0, 0, 0, 0, RetryState.Waiting);
            Assert.AreEqual(0.0, calculated);
        }

        [Test]
        public void Preparing_half_done_should_be_50_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 1000, 0, 0, RetryState.Preparing);
            Assert.AreEqual(0.5, calculated);
        }

        [Test]
        public void Preparing_done_should_be_100_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 0, 0, RetryState.Preparing);
            Assert.AreEqual(1.0, calculated);
        }

        [Test]
        public void Forwarding_half_done_should_be_50_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 1000, 0, RetryState.Forwarding);
            Assert.AreEqual(0.50, calculated);
        }

        [Test]
        public void Forwarding_done_should_be_100_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 2000, 0, RetryState.Forwarding);
            Assert.AreEqual(1.0, calculated);
        }

        [Test]
        public void Forwarding_and_skip_combination_done_should_be_100_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 1000, 1000, RetryState.Forwarding);
            Assert.AreEqual(1.0, calculated);
        }

        [Test]
        public void Skipped_done_should_be_100_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 0, 2000, RetryState.Forwarding);
            Assert.AreEqual(1.0, calculated);
        }
    }
}