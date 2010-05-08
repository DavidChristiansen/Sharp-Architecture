﻿namespace SharpArch.Data.NHibernate
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization.Formatters.Binary;
    using global::NHibernate.Cfg;


    /// <summary>
    /// File cache implementation of INHibernateConfigurationCache.  Saves and loads a
    /// seralized version of <see cref="Configuration"/> to a temporary file location.
    /// </summary>
    /// <remarks>Seralizing a <see cref="Configuration"/> object requires that all components
    /// that make up the Configuration object be Serializable.  This includes any custom NHibernate 
    /// user types implementing <see cref="NHibernate.UserTypes.IUserType"/>.</remarks>
    public class NHibernateConfigurationFileCache : INHibernateConfigurationCache
    {
        /// <summary>
        /// List of files that the cached configuration is dependent on.  If any of these
        /// files are newer than the cache file then the cache file could be out of date.
        /// </summary>
        protected List<string> dependentFilePaths = new List<string>();

        #region Constructors

        /// <summary>
        /// Initializes new instance of the NHibernateConfigurationFileCache
        /// </summary>
        public NHibernateConfigurationFileCache() { }

        /// <summary>
        /// Initializes new instance of the NHibernateConfigurationFileCache using the 
        /// given dependentFilePaths parameter.
        /// </summary>
        /// <param name="dependentFilePaths">LIst of files that the cached configuration
        /// is dependent upon.</param>
        public NHibernateConfigurationFileCache(string[] dependentFilePaths) {
            AppendToDependentFilePaths(dependentFilePaths);
        }

        #endregion

        #region INHibernateConfigurationCache Members

        /// <summary>
        /// Load the <see cref="Configuration"/> object from a cache.
        /// </summary>
        /// <param name="configKey">Key value to provide a unique name to the cached <see cref="Configuration"/>.</param>
        /// <param name="configPath">NHibernate configuration xml file.  This is used to determine if the 
        /// cached <see cref="Configuration"/> is out of date or not.</param>
        /// <returns>If an up to date cached object is available, a <see cref="Configuration"/> 
        /// object, otherwise null.</returns>
        public Configuration LoadConfiguration(string configKey, string configPath, string[] mappingAssemblies) {
            string cachePath = CachedConfigPath(configKey);
            AppendToDependentFilePaths(mappingAssemblies);
            AppendToDependentFilePaths(configPath);
            if (!IsCachedConfigCurrent(cachePath, configPath)) {
                return null;
            }

            return RetrieveFromCache(cachePath);
        }

        /// <summary>
        /// Save the <see cref="Configuration"/> object to cache to a temporary file.
        /// </summary>
        /// <param name="configKey">Key value to provide a unique name to the cached <see cref="Configuration"/>.</param>
        /// <param name="config">Configuration object to save.</param>
        public void SaveConfiguration(string configKey, Configuration config) {
            string cachePath = CachedConfigPath(configKey);
            StoreInCache(config, cachePath);
            File.SetLastWriteTime(cachePath, GetMaxDependencyDate());
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Deserializes a data file into a <see cref="Configuration"/> object.
        /// </summary>
        /// <param name="path">Full path to file containing seralized data.</param>
        /// <returns>Configuration object deseralized from data file.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the path parameter is null or empty.</exception>
        protected virtual Configuration RetrieveFromCache(string path) {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            try {
                using (FileStream file = File.Open(path, FileMode.Open)) {
                    BinaryFormatter bf = new BinaryFormatter();
                    return bf.Deserialize(file) as Configuration;
                }
            }
            catch {
                // Return null if the Configuration object can't be deseralized
                return null;
            }
        }

        /// <summary>
        /// Serialize the given Configuration object to a file at the given path.
        /// </summary>
        /// <param name="config">Configuration object to serialize.</param>
        /// <param name="path">Path of the cache file to store the serialized data.</param>
        /// <exception cref="ArgumentNullException">Thrown if the config parameter is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the path parameter is null or empty.</exception>
        protected virtual void StoreInCache(Configuration config, string path) {
            if (config == null)
                throw new ArgumentNullException("config");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            using (FileStream file = File.Open(path, FileMode.Create)) {
                new BinaryFormatter().Serialize(file, config);
            }
        }


        /// <summary>
        /// Append the given file path to the dependentFilePaths list.
        /// </summary>
        /// <param name="paths">File path.</param>
        protected virtual void AppendToDependentFilePaths(string path) {
            this.dependentFilePaths.Add(FindFile(path));
        }

        /// <summary>
        /// Append the given list of file paths to the dependentFilePaths list.
        /// </summary>
        /// <param name="paths"><see cref="IEnumerable{string}"/> list of file paths.</param>
        protected virtual void AppendToDependentFilePaths(IEnumerable<string> paths) {
            foreach (string path in paths) {
                this.dependentFilePaths.Add(FindFile(path));
            }
        }

        /// <summary>
        /// Tests if an existing cached configuration file is out of date or not.
        /// </summary>
        /// <param name="cachePath">Location of the cached</param>
        /// <param name="configPath"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if the cachePath or configPath 
        /// parameters are null.</exception>
        protected virtual bool IsCachedConfigCurrent(string cachePath, string configPath) {
            if (string.IsNullOrEmpty(cachePath))
                throw new ArgumentNullException("cachePath");
            if (string.IsNullOrEmpty(configPath))
                throw new ArgumentNullException("configPath");

            return (File.Exists(cachePath) && new FileInfo(cachePath).LastWriteTime > GetMaxDependencyDate());
        }

        /// <summary>
        /// Returns the latest date from the list of dependent file paths.
        /// </summary>
        /// <returns></returns>
        protected virtual DateTime GetMaxDependencyDate() {
            if ((this.dependentFilePaths == null) || (this.dependentFilePaths.Count == 0)) {
                return DateTime.Parse("1/1/1980");
            }

            return this.dependentFilePaths.Max(n => File.GetLastWriteTime(n));
        }

        /// <summary>
        /// Provide a unique temporary file path/name for the cache file.
        /// </summary>
        /// <param name="configKey"></param>
        /// <returns>Full file path.</returns>
        /// <remarks>The hash value is intended to avoid the file from conflicting
        /// with other applications also using this cache feature.</remarks>
        protected virtual string CachedConfigPath(string configKey) {
            var fileName = string.Format("{0}-{1}.bin", configKey, Assembly.GetCallingAssembly().CodeBase.GetHashCode());
            return Path.Combine(Path.GetTempPath(), fileName);
        }

        /// <summary>
        /// Tests if the file or assembly name exists either in the application's bin folder
        /// or elsewhere.
        /// </summary>
        /// <param name="path">Path or file name to test for existance.</param>
        /// <returns>Full path of the file.</returns>
        /// <remarks>If the path parameter does not end with ".dll" it is appended and 
        /// tested if the dll file exists.</remarks>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
        private string FindFile(string path) {
            if (File.Exists(path)) {
                return path;
            }

            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string uriPath = Uri.UnescapeDataString(uri.Path);
            string codeLocation = Path.GetDirectoryName(uriPath);

            string codePath = Path.Combine(codeLocation, path);
            if (File.Exists(codePath)) {
                return codePath;
            }

            string dllPath = (path.IndexOf(".dll") == -1) ? path.Trim() + ".dll" : path.Trim();
            if (File.Exists(dllPath)) {
                return dllPath;
            }

            string codeDllPath = Path.Combine(codeLocation, dllPath);
            if (File.Exists(codeDllPath)) {
                return codeDllPath;
            }

            throw new FileNotFoundException("Unable to find file.", path);
        }

        #endregion
    }
}
