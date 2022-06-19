namespace Examples.Chapter15
{
    public class Tests
    {
        private static StatefulComputation<int, int> GetAndIncrement
            = count =>
            {
                return (count, count + 1);
            };

        [Test]
        public void TestState()
        {
            var q = from value1 in GetAndIncrement
                    from value2 in GetAndIncrement
                    from value3 in GetAndIncrement
                    select value1 + value2 + value3;
            var result = q(0).Value;
            Assert.AreEqual(3, result);
        }
    }
}
