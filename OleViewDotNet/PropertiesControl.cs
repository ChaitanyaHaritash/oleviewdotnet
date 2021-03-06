﻿//    This file is part of OleViewDotNet.
//    Copyright (C) James Forshaw 2014, 2016
//
//    OleViewDotNet is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    OleViewDotNet is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with OleViewDotNet.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace OleViewDotNet
{
    public partial class PropertiesControl : UserControl
    {
        private COMRegistry m_registry;
        private COMAppIDEntry m_appid;
        private COMCLSIDEntry m_clsid;
        private COMInterfaceEntry m_interface;
        private COMTypeLibVersionEntry m_typelib;
        private COMProcessEntry m_process;
        private COMRuntimeClassEntry m_runtime_class;
        private COMRuntimeServerEntry m_runtime_server;

        private void LoadInterfaceList(IEnumerable<COMInterfaceInstance> entries, ListView view)
        {
            view.Items.Clear();
            foreach (Tuple<COMInterfaceInstance, COMInterfaceEntry> entry in
                entries.Select(e => new Tuple<COMInterfaceInstance, COMInterfaceEntry>(e, m_registry.MapIidToInterface(e.Iid))).OrderBy(e => e.Item2.Name))
            {
                ListViewItem item = view.Items.Add(entry.Item2.Name);
                item.SubItems.Add(entry.Item1.Iid.FormatGuid());
                item.SubItems.Add(entry.Item2.NumMethods.ToString());
                if (!string.IsNullOrWhiteSpace(entry.Item1.Module))
                {
                    item.SubItems.Add(string.Format("{0}+0x{1:X}",
                        entry.Item1.Module, entry.Item1.VTableOffset));
                }
                item.Tag = entry;
            }
            view.ListViewItemSorter = new ListItemComparer(0);
            view.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            view.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private static string GetStringValue(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "N/A" : value;
        }

        private static string GetGuidValue(Guid guid)
        {
            return guid == Guid.Empty ? "N/A" : guid.FormatGuid();
        }

        private void SetupAppIdEntry(COMAppIDEntry entry)
        {
            textBoxAppIdName.Text = entry.Name;
            textBoxAppIdGuid.Text = entry.AppId.FormatGuid();
            textBoxLaunchPermission.Text = entry.LaunchPermission;
            textBoxAccessPermission.Text = entry.AccessPermission;
            textBoxAppIDRunAs.Text = GetStringValue(entry.RunAs);
            textBoxAppIDService.Text = GetStringValue(entry.IsService ? entry.LocalService.Name : null);
            textBoxAppIDFlags.Text = entry.Flags.ToString();
            textBoxDllSurrogate.Text = GetStringValue(entry.DllSurrogate);
            btnViewAccessPermissions.Enabled = entry.HasAccessPermission;
            btnViewLaunchPermissions.Enabled = entry.HasLaunchPermission;
            tabControlProperties.TabPages.Add(tabPageAppID);

            if (entry.IsService)
            {
                textBoxServiceName.Text = entry.LocalService.Name;
                textBoxServiceDisplayName.Text = GetStringValue(entry.LocalService.DisplayName);
                textBoxServiceType.Text = entry.LocalService.ServiceType.ToString();
                textBoxServiceImagePath.Text = entry.LocalService.ImagePath;
                textBoxServiceDll.Text = GetStringValue(entry.LocalService.ServiceDll);
                textBoxServiceUserName.Text = GetStringValue(entry.LocalService.UserName);
                tabControlProperties.TabPages.Add(tabPageService);
            }

            m_appid = entry;
        }

        private void SetupClsidEntry(COMCLSIDEntry entry)
        {
            textBoxClsidName.Text = entry.Name;
            textBoxClsid.Text = entry.Clsid.FormatGuid();
            textBoxServerType.Text = entry.DefaultServerType.ToString();
            textBoxThreadingModel.Text = entry.DefaultThreadingModel.ToString();
            textBoxServer.Text = entry.DefaultServer;
            textBoxCmdLine.Text = GetStringValue(entry.DefaultCmdLine);
            textBoxTreatAs.Text = GetGuidValue(entry.TreatAs);
            btnTreatAsProps.Enabled = m_registry.Clsids.ContainsKey(entry.TreatAs);
            var progids = m_registry.Progids;

            foreach (string progid in m_registry.GetProgIdsForClsid(entry.Clsid).Select(p => p.ProgID))
            {
                ListViewItem item = listViewProgIDs.Items.Add(progid);
                if (progids.ContainsKey(progid))
                {
                    item.Tag = progids[progid];
                }
            }
            listViewProgIDs.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

            foreach (Guid catid in entry.Categories)
            {
                ListViewItem item = listViewCategories.Items.Add(COMUtilities.GetCategoryName(catid));
                item.Tag = catid;
            }
            listViewCategories.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

            LoadInterfaceList(entry.Interfaces, listViewInterfaces);
            LoadInterfaceList(entry.FactoryInterfaces, listViewFactoryInterfaces);
            tabPageSupportedInterfaces.Tag = entry;

            tabControlProperties.TabPages.Add(tabPageClsid);
            tabControlProperties.TabPages.Add(tabPageSupportedInterfaces);
            if (m_registry.AppIDs.ContainsKey(entry.AppID))
            {
                SetupAppIdEntry(m_registry.AppIDs[entry.AppID]);
            }

            IEnumerable<COMInterfaceEntry> proxies = m_registry.GetProxiesForClsid(entry);
            if (proxies.Count() > 0)
            {
                foreach (COMInterfaceEntry intf in proxies.OrderBy(i => i.Name))
                {
                    ListViewItem item = listViewProxies.Items.Add(intf.Name);
                    item.SubItems.Add(intf.Iid.FormatGuid());
                    item.Tag = intf;
                }
                listViewProxies.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                listViewProxies.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                listViewProxies.ListViewItemSorter = new ListItemComparer(0);
                tabControlProperties.TabPages.Add(tabPageProxies);
            }

            if (entry.Servers.Count > 1)
            {
                foreach (COMCLSIDServerEntry server in entry.Servers.Values)
                {
                    ListViewItem item = listViewCLSIDServers.Items.Add(server.ServerType.ToString());
                    item.SubItems.Add(server.Server);
                    item.SubItems.Add(server.CommandLine);
                    item.SubItems.Add(server.ThreadingModel.ToString());
                    item.Tag = server;
                }

                listViewCLSIDServers.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                listViewCLSIDServers.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                listViewCLSIDServers.ListViewItemSorter = new ListItemComparer(0);
                tabControlProperties.TabPages.Add(tabPageServers);
            }

            SetupTypeLibVersionEntry(m_registry.GetTypeLibVersionEntry(entry.TypeLib, null));

            if (entry.Elevation != null)
            {
                textBoxElevationEnabled.Text = entry.Elevation.Enabled.ToString();
                textBoxElevationAutoApproval.Text = entry.Elevation.AutoApproval.ToString();
                textBoxElevationIconReference.Text = GetStringValue(entry.Elevation.IconReference);
                foreach (COMCLSIDEntry vso in entry.Elevation.VirtualServerObjects.Select(v => m_registry.MapClsidToEntry(v)))
                {
                    ListViewItem item = listViewElevationVSOs.Items.Add(vso.Name);
                    item.SubItems.Add(vso.Clsid.ToString());
                    item.SubItems.Add(vso.CanElevate.ToString());
                    item.SubItems.Add(vso.AutoElevation.ToString());
                    item.Tag = vso;
                }
                listViewElevationVSOs.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                listViewElevationVSOs.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                listViewElevationVSOs.ListViewItemSorter = new ListItemComparer(0);
                tabControlProperties.TabPages.Add(tabPageElevation);
            }

            if (entry.Servers.ContainsKey(COMServerType.InProcServer32) && entry.Servers[COMServerType.InProcServer32].HasDotNet)
            {
                COMCLSIDServerDotNetEntry dotnet = entry.Servers[COMServerType.InProcServer32].DotNet;
                textBoxDotNetAssemblyName.Text = dotnet.AssemblyName;
                textBoxDotNetClassName.Text = dotnet.ClassName;
                textBoxDotNetCodeBase.Text = dotnet.CodeBase;
                textBoxDotNetRuntimeVersion.Text = dotnet.RuntimeVersion;
                tabControlProperties.TabPages.Add(tabPageDotNet);
            }

            m_clsid = entry;
        }

        private void SetupInterfaceEntry(COMInterfaceEntry entry)
        {
            textBoxInterfaceName.Text = entry.Name;
            textBoxIID.Text = GetGuidValue(entry.Iid);
            textBoxInterfaceBase.Text = GetStringValue(entry.Base);
            textBoxInterfaceProxy.Text = GetGuidValue(entry.ProxyClsid);
            txtMethods.Text = entry.NumMethods.ToString();
            btnProxyProperties.Enabled = m_registry.Clsids.ContainsKey(entry.ProxyClsid);
            tabControlProperties.TabPages.Add(tabPageInterface);
            SetupTypeLibVersionEntry(m_registry.GetTypeLibVersionEntry(entry.TypeLib, entry.TypeLibVersion));
            m_interface = entry;
        }

        private void SetupRuntimeServerEntry(COMRuntimeServerEntry entry)
        {
            textBoxRuntimeServerName.Text = entry.Name;
            textBoxRuntimeServerExePath.Text = GetStringValue(entry.ExePath);
            textBoxRuntimeServerPermissions.Text = GetStringValue(entry.Permissions);
            btnRuntimeServerViewPermissions.Enabled = entry.HasPermission;
            textBoxRuntimeServerServiceName.Text = GetStringValue(entry.ServiceName);
            textBoxRuntimeServerType.Text = entry.ServerType.ToString();
            textBoxRuntimeServerIdentity.Text = GetStringValue(entry.Identity);
            textBoxRuntimeServerIdentityType.Text = entry.IdentityType.ToString();
            textBoxRuntimeServerInstancing.Text = entry.InstancingType.ToString();
            m_runtime_server = entry;
            tabControlProperties.TabPages.Add(tabPageRuntimeServer);
        }

        private void SetupRuntimeClassEntry(COMRuntimeClassEntry entry)
        {
            textBoxRuntimeClassName.Text = entry.Name;
            textBoxRuntimeClassCLSID.Text = GetGuidValue(entry.Clsid);
            textBoxRuntimeClassServer.Text = GetStringValue(entry.Server);
            textBoxRuntimeClassPermissions.Text = GetStringValue(entry.Permissions);
            textBoxRuntimeClassDllPath.Text = GetStringValue(entry.DllPath);
            textBoxRuntimeClassActivationType.Text = entry.ActivationType.ToString();
            textBoxRuntimeClassTrustLevel.Text = entry.TrustLevel.ToString();
            textBoxRuntimeClassThreading.Text = entry.Threading.ToString();
            LoadInterfaceList(entry.Interfaces, listViewInterfaces);
            LoadInterfaceList(entry.FactoryInterfaces, listViewFactoryInterfaces);
            btnRuntimeClassViewPermissions.Enabled = entry.HasPermission;
            tabPageSupportedInterfaces.Tag = entry;
            m_runtime_class = entry;
            tabControlProperties.TabPages.Add(tabPageRuntimeClass);
            tabControlProperties.TabPages.Add(tabPageSupportedInterfaces);
            COMRuntimeServerEntry server = m_registry.MapRuntimeClassToServerEntry(entry);
            if (server != null)
            {
                SetupRuntimeServerEntry(server);
            }
        }

        private void SetupProperties(object obj)
        {
            if (obj is COMCLSIDEntry)
            {
                SetupClsidEntry((COMCLSIDEntry)obj);
            }

            if (obj is COMProgIDEntry)
            {
                COMProgIDEntry entry = (COMProgIDEntry)obj;
                COMCLSIDEntry clsid_entry = m_registry.MapClsidToEntry(entry.Clsid);
                SetupClsidEntry(clsid_entry);
            }

            if (obj is COMAppIDEntry)
            {
                SetupAppIdEntry((COMAppIDEntry)obj);
            }

            if (obj is COMInterfaceEntry)
            {
                SetupInterfaceEntry((COMInterfaceEntry)obj);
            }

            if (obj is COMTypeLibVersionEntry)
            {
                SetupTypeLibVersionEntry((COMTypeLibVersionEntry)obj);
            }

            if (obj is COMProcessEntry)
            {
                SetupProcessEntry((COMProcessEntry)obj);
            }

            if (obj is COMIPIDEntry)
            {
                SetupIPIDEntry((COMIPIDEntry)obj);
            }

            if (obj is COMRuntimeClassEntry)
            {
                SetupRuntimeClassEntry((COMRuntimeClassEntry)obj);
            }

            if (obj is COMRuntimeServerEntry)
            {
                SetupRuntimeServerEntry((COMRuntimeServerEntry)obj);
            }
        }

        private void SetupIpidEntries(IEnumerable<COMIPIDEntry> ipids, bool show_disconnected)
        {
            listViewProcessIPids.Items.Clear();
            listViewProcessIPids.Items.AddRange(ipids.Where(ipid => ipid.IsRunning || show_disconnected).Select(ipid =>
            {
                ListViewItem item = new ListViewItem(ipid.Ipid.ToString());
                item.SubItems.Add(m_registry.MapIidToInterface(ipid.Iid).Name);
                item.SubItems.Add(ipid.Flags.ToString());
                item.Tag = ipid;
                return item;
            }).ToArray());
            listViewProcessIPids.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            listViewProcessIPids.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void SetupProcessEntry(COMProcessEntry obj)
        {
            m_process = obj;
            textBoxProcessExecutablePath.Text = obj.ExecutablePath;
            textBoxProcessProcessId.Text = obj.Pid.ToString();
            textBoxProcessAppId.Text = GetGuidValue(obj.AppId);
            textBoxProcessAccessPermissions.Text = GetStringValue(obj.AccessPermissions);
            btnProcessViewAccessPermissions.Enabled = !String.IsNullOrWhiteSpace(obj.AccessPermissions);
            textBoxProcessLrpcPermissions.Text = GetStringValue(obj.LRpcPermissions);
            textBoxProcessUser.Text = GetStringValue(obj.User);
            textBoxProcessSecurity.Text = String.Format("Capabilities: {0}, Authn Level: {1}, Imp Level: {2}",
                obj.Capabilities, obj.AuthnLevel, obj.ImpLevel);
            textBoxProcessStaHwnd.Text = String.Format("0x{0:X}", obj.STAMainHWnd.ToInt64());
            SetupIpidEntries(obj.Ipids, false);
            listViewProcessIPids.ListViewItemSorter = new ListItemComparer(0);
            tabControlProperties.TabPages.Add(tabPageProcess);
            if (m_registry.AppIDs.ContainsKey(obj.AppId))
            {
                SetupAppIdEntry((COMAppIDEntry)m_registry.AppIDs[obj.AppId]);
            }
        }

        private void SetupIPIDEntry(COMIPIDEntry obj)
        {
            textBoxIPID.Text = obj.Ipid.FormatGuid();
            textBoxIPIDIID.Text = obj.Iid.FormatGuid();
            textBoxIPIDIIDName.Text = m_registry.MapIidToInterface(obj.Iid).Name;
            textBoxIPIDFlags.Text = obj.Flags.ToString();
            textBoxIPIDInterface.Text = String.Format("0x{0:X}", obj.Interface.ToInt64());
            textBoxIPIDInterfaceVTable.Text = GetStringValue(obj.InterfaceVTable);
            textBoxIPIDStub.Text = String.Format("0x{0:X}", obj.Stub.ToInt64());
            textBoxIPIDStubVTable.Text = GetStringValue(obj.StubVTable);
            textBoxIPIDOXID.Text = obj.Oxid.FormatGuid();
            textBoxIPIDReferences.Text = String.Format("Strong: {0}, Weak: {1}, Private: {2}",
                obj.StrongRefs, obj.WeakRefs, obj.PrivateRefs);
            
            textBoxIPIDProcessId.Text = COMUtilities.GetProcessIdFromIPid(obj.Ipid).ToString();
            textBoxIPIDApartment.Text = COMUtilities.GetApartmentIdStringFromIPid(obj.Ipid);
            textBoxIPIDStaHwnd.Text = String.Format("0x{0:X}", obj.ServerSTAHwnd.ToInt64());
            tabControlProperties.TabPages.Add(tabPageIPID);
        }

        private void SetupTypeLibVersionEntry(COMTypeLibVersionEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            textBoxTypeLibName.Text = entry.Name;
            textBoxTypeLibId.Text = GetGuidValue(entry.TypelibId);
            textBoxTypeLibVersion.Text = entry.Version;
            textBoxTypeLibWin32.Text = GetStringValue(entry.Win32Path);
            textBoxTypeLibWin64.Text = GetStringValue(entry.Win64Path);
            m_typelib = entry;
            tabControlProperties.TabPages.Add(tabPageTypeLib);
        }

        public static bool SupportsProperties(object obj)
        {
            return obj is COMCLSIDEntry || obj is COMProgIDEntry || obj is COMAppIDEntry
                || obj is COMInterfaceEntry || obj is COMTypeLibVersionEntry || obj is COMProcessEntry
                || obj is COMIPIDEntry || obj is COMRuntimeClassEntry || obj is COMRuntimeServerEntry;
        }

        public PropertiesControl(COMRegistry registry, string name, object obj)
        {
            m_registry = registry;
            InitializeComponent();
            listViewCategories.Columns.Add("Name", 100);
            listViewProgIDs.Columns.Add("Name", 100);
            listViewInterfaces.Columns.Add("Name", 100);
            listViewInterfaces.Columns.Add("IID", 100);
            listViewInterfaces.Columns.Add("Methods", 100);
            listViewInterfaces.Columns.Add("VTable Offset", 100);
            listViewFactoryInterfaces.Columns.Add("Name", 100);
            listViewFactoryInterfaces.Columns.Add("IID", 100);
            listViewFactoryInterfaces.Columns.Add("Methods", 100);
            listViewFactoryInterfaces.Columns.Add("VTable Offset", 100);
            tabControlProperties.TabPages.Clear();
            SetupProperties(obj);
            if (tabControlProperties.TabCount == 0)
            {
                tabControlProperties.TabPages.Add(tabPageNoProperties);
            }
            this.Text = String.Format("{0} Properties", name.Replace("&", "&&"));
        }

        private async void btnRefreshInterfaces_Click(object sender, EventArgs e)
        {
            try
            {
                ICOMClassEntry entry = (ICOMClassEntry)tabPageSupportedInterfaces.Tag;
                await entry.LoadSupportedInterfacesAsync(true);
                LoadInterfaceList(entry.Interfaces, listViewInterfaces);
                LoadInterfaceList(entry.FactoryInterfaces, listViewFactoryInterfaces);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnViewLaunchPermissions_Click(object sender, EventArgs e)
        {
            COMSecurity.ViewSecurity(m_registry, m_appid, false);
        }

        private void btnViewAccessPermissions_Click(object sender, EventArgs e)
        {
            COMSecurity.ViewSecurity(m_registry, m_appid, true);
        }

        private void copyProgIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewProgIDs.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewProgIDs.SelectedItems[0];
                COMRegistryViewer.CopyTextToClipboard(item.Text);
            }
        }

        private void btnTreatAsProps_Click(object sender, EventArgs e)
        {
            if (m_registry.Clsids.ContainsKey(m_clsid.TreatAs))
            {
                Program.GetMainForm(m_registry).HostControl(new PropertiesControl(m_registry, 
                    m_clsid.Name, m_registry.Clsids[m_clsid.TreatAs]));
            }
        }

        private async void btnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                object comObj = m_clsid.CreateInstanceAsObject(m_clsid.CreateContext, null);
                if (comObj != null)
                {
                    await Program.GetMainForm(m_registry).HostObject(m_clsid, comObj, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnProxyProperties_Click(object sender, EventArgs e)
        {
            if (m_registry.Clsids.ContainsKey(m_interface.ProxyClsid))
            {
                COMCLSIDEntry entry = m_registry.Clsids[m_interface.ProxyClsid];
                Program.GetMainForm(m_registry).HostControl(new PropertiesControl(m_registry,
                    entry.Name, entry));
            }
        }

        private void btnOpenTypeLib_Click(object sender, EventArgs e)
        {
            if (m_typelib != null)
            {
                Assembly typelib = COMUtilities.LoadTypeLib(this, m_typelib.NativePath);
                if (typelib != null)
                {
                    Program.GetMainForm(m_registry).HostControl(new TypeLibControl(m_typelib.Name, 
                        typelib, m_interface != null ? m_interface.Iid : Guid.Empty, false));
                }
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListView view = sender as ListView;
            if (view != null && view.SelectedIndices.Count > 0)
            {
                ListViewItem item = view.SelectedItems[0];
                COMRegistryViewer.CopyTextToClipboard(item.Text);
            }
        }

        private void CopyIID(ListView view, COMRegistryViewer.CopyGuidType type)
        {
            if (view != null && view.SelectedIndices.Count > 0)
            {
                ListViewItem item = view.SelectedItems[0];
                Tuple<COMInterfaceInstance, COMInterfaceEntry> intf = item.Tag as Tuple<COMInterfaceInstance, COMInterfaceEntry>;
                COMRegistryViewer.CopyGuidToClipboard(intf.Item1.Iid, type);
            }
        }

        private void asStringToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            CopyIID(GetListViewForMenu(sender), COMRegistryViewer.CopyGuidType.CopyAsString);
        }

        private void asCStructureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyIID(GetListViewForMenu(sender), COMRegistryViewer.CopyGuidType.CopyAsStructure);
        }

        private void asHexStringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyIID(GetListViewForMenu(sender), COMRegistryViewer.CopyGuidType.CopyAsHexString);
        }

        private ListView GetListViewForMenu(object sender)
        {
            ContextMenuStrip menu = sender as ContextMenuStrip;
            ToolStripMenuItem item = sender as ToolStripMenuItem;

            if (item != null)
            {
                menu = item.Owner as ContextMenuStrip; 
            }

            if (menu != null)
            {
                return menu.SourceControl as ListView;
            }
            return null;
        }

        private void contextMenuStripInterfaces_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ListView view = GetListViewForMenu(sender);
            if (view != null && view.SelectedIndices.Count > 0)
            {
                ListViewItem item = view.SelectedItems[0];
                Tuple<COMInterfaceInstance, COMInterfaceEntry> intf = 
                    item.Tag as Tuple<COMInterfaceInstance, COMInterfaceEntry>;
                viewProxyDefinitionToolStripMenuItem.Enabled = m_registry.Clsids.ContainsKey(intf.Item2.ProxyClsid);
            }
            else
            {
                viewProxyDefinitionToolStripMenuItem.Enabled = false;
            }
        }

        private void viewProxyDefinitionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ListView view = GetListViewForMenu(sender);
                if (view != null && view.SelectedIndices.Count > 0)
                {
                    ListViewItem item = view.SelectedItems[0];
                    Tuple<COMInterfaceInstance, COMInterfaceEntry> intf =
                        item.Tag as Tuple<COMInterfaceInstance, COMInterfaceEntry>;

                    if (m_registry.Clsids.ContainsKey(intf.Item2.ProxyClsid))
                    {
                        COMCLSIDEntry clsid = m_registry.Clsids[intf.Item2.ProxyClsid];
                        Program.GetMainForm(m_registry).HostControl(new TypeLibControl(m_registry,
                            Path.GetFileName(clsid.DefaultServer), COMProxyInstance.GetFromCLSID(clsid), intf.Item1.Iid));
                    }
                }
            }
            catch (Exception ex)
            {
                Program.ShowError(this, ex);
            }
        }

        private void btnProcessViewAccessPermissions_Click(object sender, EventArgs e)
        {
            COMSecurity.ViewSecurity(m_registry, String.Format("{0} Access", m_process.Name), 
                m_process.AccessPermissions, true);
        }

        private COMIPIDEntry GetSelectedIpid()
        {
            if (listViewProcessIPids.SelectedItems.Count > 0)
            {
                return (COMIPIDEntry)listViewProcessIPids.SelectedItems[0].Tag;
            }
            return null;
        }

        private void copyInterfacePointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            COMIPIDEntry ipid = GetSelectedIpid();
            if (ipid != null)
            {
                COMRegistryViewer.CopyTextToClipboard(String.Format("0x{0:X}", ipid.Interface.ToInt64()));
            }
        }

        private void copyStubPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            COMIPIDEntry ipid = GetSelectedIpid();
            if (ipid != null)
            {
                COMRegistryViewer.CopyTextToClipboard(String.Format("0x{0:X}", ipid.Stub.ToInt64()));
            }
        }

        private void toHexEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            COMIPIDEntry ipid = GetSelectedIpid();
            if (ipid != null)
            {
                Program.GetMainForm(m_registry).HostControl(new ObjectHexEditor(m_registry, ipid.Ipid.ToString(), ipid.ToObjref()));
            }
        }

        private void toFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            COMIPIDEntry ipid = GetSelectedIpid();
            if (ipid != null)
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Filter = "All Files (*.*)|*.*";
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            File.WriteAllBytes(dlg.FileName, ipid.ToObjref());
                        }
                        catch (Exception ex)
                        {
                            Program.ShowError(this, ex);
                        }
                    }
                }
            }
        }

        private async void toObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            COMIPIDEntry ipid = GetSelectedIpid();
            if (ipid != null)
            {
                try
                {
                    await Program.GetMainForm(m_registry).OpenObjectInformation(
                        COMUtilities.UnmarshalObject(ipid.ToObjref()), 
                        String.Format("IPID {0}", ipid.Ipid));
                }
                catch (Exception ex)
                {
                    Program.ShowError(this, ex);
                }
            }
        }

        private void listViewElevationVSOs_DoubleClick(object sender, EventArgs e)
        {
            if (listViewElevationVSOs.SelectedItems.Count < 1)
            {
                return;
            }

            COMCLSIDEntry clsid = listViewElevationVSOs.SelectedItems[0].Tag as COMCLSIDEntry;
            if (clsid != null)
            {
                Program.GetMainForm(m_registry).HostControl(new PropertiesControl(m_registry,
                        clsid.Name, clsid));
            }
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListView list_view = sender as ListView;
            if (list_view != null && list_view.SelectedItems.Count > 0 && list_view.SelectedItems[0].Tag != null)
            {
                Program.GetMainForm(m_registry).UpdatePropertyGrid(list_view.SelectedItems[0].Tag);
            }
        }

        private void listView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListItemComparer.UpdateListComparer(sender as ListView, e.Column);
        }

        private void btnViewAssembly_Click(object sender, EventArgs e)
        {
            try
            {
                Assembly asm = null;

                if (!string.IsNullOrWhiteSpace(textBoxDotNetCodeBase.Text))
                {
                    asm = Assembly.LoadFrom(textBoxDotNetCodeBase.Text);
                }
                else
                {
                    asm = Assembly.Load(textBoxDotNetAssemblyName.Text);
                }

                Program.GetMainForm(m_registry).HostControl(new TypeLibControl(asm.GetName().Name,
                        asm, m_clsid != null ? m_clsid.Clsid : Guid.Empty, true));
            }
            catch(Exception ex)
            {
                Program.ShowError(this, ex);
            }
        }

        private void checkBoxShowDisconnected_CheckedChanged(object sender, EventArgs e)
        {
            SetupIpidEntries(m_process.Ipids, checkBoxShowDisconnected.Checked);
        }

        private void btnRuntimeClassViewPermissions_Click(object sender, EventArgs e)
        {
            COMSecurity.ViewSecurity(m_registry, string.Format("{0} Permissions", m_runtime_class.Name), m_runtime_class.Permissions, false);
        }

        private void btnRuntimeServerViewPermissions_Click(object sender, EventArgs e)
        {
            COMSecurity.ViewSecurity(m_registry, string.Format("{0} Permissions", m_runtime_server.Name), m_runtime_server.Permissions, false);
        }
    }
}
