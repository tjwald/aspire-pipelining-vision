using Aspire.Hosting.JavaScript;

internal static class ResourceExtensions
{
    extension(IResource resource)
    {
        /// Get Working Directory from ExecutableAnnotation
        public string WorkingDirectory
        {
            get 
            {
                if (resource.TryGetLastAnnotation<ExecutableAnnotation>(out var execAnnotation))
                {
                    return execAnnotation.WorkingDirectory;
                }
                throw new InvalidOperationException($"Could not find working directory for {resource.Name}");
            }
        }

        public string PackageManager
        {
            get 
            {
                if (resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var packageManagerAnnotation))
                {
                    return packageManagerAnnotation.ExecutableName;
                }
                throw new InvalidOperationException($"Could not find package manager for {resource.Name}");
            }
        }
    }
}