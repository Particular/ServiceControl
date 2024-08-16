namespace ServiceControl.UnitTests.Operations
{
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryOperationProgressCalculatorTests
    {
        [Test]
        public void Completed_should_be_100_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 2000, 2000, 0, RetryState.Completed);
            Assert.That(calculated, Is.EqualTo(1.0));
        }

        [Test]
        public void Waiting_should_be_0_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(0, 0, 0, 0, RetryState.Waiting);
            Assert.That(calculated, Is.EqualTo(0.0));
        }

        [Test]
        public void Preparing_half_done_should_be_50_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 1000, 0, 0, RetryState.Preparing);
            Assert.That(calculated, Is.EqualTo(0.5));
        }

        [Test]
        public void Preparing_done_should_be_100_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 2000, 0, 0, RetryState.Preparing);
            Assert.That(calculated, Is.EqualTo(1.0));
        }

        [Test]
        public void Forwarding_half_done_should_be_50_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 2000, 1000, 0, RetryState.Forwarding);
            Assert.That(calculated, Is.EqualTo(0.50));
        }

        [Test]
        public void Forwarding_done_should_be_100_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 2000, 2000, 0, RetryState.Forwarding);
            Assert.That(calculated, Is.EqualTo(1.0));
        }

        [Test]
        public void Forwarding_and_skip_combination_done_should_be_100_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 2000, 1000, 1000, RetryState.Forwarding);
            Assert.That(calculated, Is.EqualTo(1.0));
        }

        [Test]
        public void Skipped_done_should_be_100_percentage()
        {
            var calculated = OperationProgressCalculator.CalculateProgress(2000, 2000, 0, 2000, RetryState.Forwarding);
            Assert.That(calculated, Is.EqualTo(1.0));
        }
    }
}