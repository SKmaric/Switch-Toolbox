using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library.NodeWrappers;
using Toolbox.Library;
using System.Windows.Forms;

namespace FirstPlugin.NodeWrappers
{
    public class SiffWrapper : STGenericWrapper, IContextMenuNode
    {
        public override void OnClick(TreeView treeview)
        {
            base.OnClick(treeview);
        }

    }
}
