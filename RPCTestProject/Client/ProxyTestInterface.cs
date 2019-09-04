namespace Client
{
    using TestLibrary;

    public class ProxyTestInterface : ITestClassInterface
    {
        public ProxyTestInterface(ITestClassInterface instance)
        {
            _instance = instance;
        }

        private ITestClassInterface _instance;

        public int Id => _instance.Id;

        public string Name => _instance.Name;
    }
}