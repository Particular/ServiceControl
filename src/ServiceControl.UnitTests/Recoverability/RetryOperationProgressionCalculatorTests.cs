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
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 2000);
            Assert.AreEqual(1.0, calculated);
        }

        [Test]
        public void Waiting_should_be_5_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(0, 0, 0);
            Assert.AreEqual(0.05, calculated);
        }

        [Test]
        public void Preparing_should_be_28point75_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 1000, 0);
            Assert.AreEqual(0.2875, calculated);
        }

        [Test]
        public void Preparing_should_be_52point5_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 0);
            Assert.AreEqual(0.525, calculated);
        }

        [Test]
        public void Forwarding_should_be_76point25_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 1000);
            Assert.AreEqual(0.7625, calculated);
        }

        [Test]
        public void Forwarding_should_be_100_percentage()
        {
            var calculated = RetryOperationProgressionCalculator.CalculateProgression(2000, 2000, 2000);
            Assert.AreEqual(1.0, calculated);
        }
    }
}