namespace PearlCLR.JIT
{
    public enum MetadataTypeHandlingMode
    {
        /// <summary>
        /// No TypeHandle metadata appended to reference type. This means, if you cast a reference type to object
        /// and attempt to call ToString on it, it would fail since there is no underlying type information
        /// to find the appropriate method to call for that class. 
        /// </summary>
        None,
        Full_Fixed,
        Full_Native,
        
    }
}