using System;
using System.IO;

namespace mj.compiler.main
{
    public class SourceFile
    {
        private String path;

        public SourceFile(string path)
        {
            this.path = path;
        }

        public Stream openInput()
        {
            return new FileStream(path, FileMode.Open);
        }

        public string Path => path;
    }
}
