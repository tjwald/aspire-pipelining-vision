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

        // Since resources lose their typing in pipeline - I have to put it in a generic extension and not typed to a JavaScript app
        public string PackageManager 
            => resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var packageManagerAnnotation) 
                ? packageManagerAnnotation.ExecutableName 
                : throw new InvalidOperationException($"Could not find package manager for {resource.Name}");
    }
}