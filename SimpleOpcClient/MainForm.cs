using System;
using System.Windows.Forms;
using Opc.Ua.Client;
using Opc.Ua;

namespace SimpleOpcClient
{
    public partial class MainForm : Form
    {
        private Session _session;
        private ApplicationConfiguration _configuration;
        private Browser _browser;

        public MainForm()
        {
            InitializeComponent();

            _configuration = new ApplicationConfiguration
            {
                ApplicationType = ApplicationType.Client,
                CertificateValidator = new CertificateValidator(),
                ClientConfiguration = new ClientConfiguration()
            };

            this.tbServerUrl.Text = "opc.tcp://localhost:4848";
            tsSessionState.Text = "Disconnected";
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                this.btnDisconnect_Click(this, EventArgs.Empty);

                EndpointDescription endpointDescription = SelectEndpoint(tbServerUrl.Text, false);
                EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(null);
                ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                _session = Session.Create(_configuration, endpoint, false, true, Application.ProductName, 60000, null, null);

                tsSessionState.Text = _session.Connected == false ? "Disconnected" : "Connected";
            }
            catch (ServiceResultException ex)
            {
                tsSessionState.Text = ex.Message;
            }
            catch (UriFormatException ex)
            {
                tsSessionState.Text = ex.Message;
            }
        }

        private EndpointDescription SelectEndpoint(string discoveryUrl, bool useSecurity)
        {
            Uri uri = new Uri(discoveryUrl);
            EndpointDescription selectedEndpoint = null;

            using (DiscoveryClient client = DiscoveryClient.Create(uri))
            {
                EndpointDescriptionCollection endpoints = client.GetEndpoints(null);

                for (int i = 0; i < endpoints.Count; i++)
                {
                    EndpointDescription endpoint = endpoints[i];

                    if (endpoint.EndpointUrl.StartsWith(uri.Scheme))
                    {
                        if (useSecurity)
                        {
                            if (endpoint.SecurityMode == MessageSecurityMode.None)
                                continue;
                        }
                        else
                        {
                            if (endpoint.SecurityMode != MessageSecurityMode.None)
                                continue;
                        }

                        if (selectedEndpoint == null)
                            selectedEndpoint = endpoint;

                        if (endpoint.SecurityLevel > selectedEndpoint.SecurityLevel)
                            selectedEndpoint = endpoint;
                    }
                }

                if (selectedEndpoint == null && endpoints.Count > 0)
                    selectedEndpoint = endpoints[0];
                return selectedEndpoint;
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (_session != null)
            {
                _session.Close(10000);
                tsSessionState.Text = _session.Connected == false ? "Disconnected" : "Connected";
                _session = null;

                tvBrowse.Nodes.Clear();
                tbReference.Clear();
            }
        }

        private void btnBrowseNodes_Click(object sender, EventArgs e)
        {
            try
            {
                NodeId rootNodeId = ObjectIds.ObjectsFolder;
                INode node = _session.NodeCache.Find(rootNodeId);

                _browser = new Browser(_session)
                {
                    BrowseDirection = BrowseDirection.Forward,
                    ContinueUntilDone = true,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences
                };

                TreeNode treeNode = tvBrowse.Nodes.Add(node.ToString());
                treeNode.Tag = new ReferenceDescription { NodeId = node.NodeId };
                treeNode.Nodes.Add(string.Empty);   //To display the plus/minus button
                treeNode.Expand();
            }
            catch (NullReferenceException)
            {
                tsSessionState.Text = "Connection not established.";
            }
        }

        private void tvBrowse_AfterSelect(object sender, TreeViewEventArgs e)
        {
            ReferenceDescription reference = (ReferenceDescription)e.Node.Tag;
            NodeId node = (NodeId)reference.NodeId;

            Browser browser = new Browser(_session)
            {
                ContinueUntilDone = true,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.References
            };

            ReferenceDescriptionCollection rdc = browser.Browse(node);

            string nodeType = rdc[0].BrowseName.Name;
            string id = node.Identifier.ToString();
            tbReference.Text = string.Format("Identifier: {1}\r\nHasTypeDefinition: {0}", nodeType, id);
        }

        private void tvBrowse_AfterExpand(object sender, TreeViewEventArgs e)
        {
            e.Node.Nodes.Clear();
            ReferenceDescription rd = e.Node.Tag as ReferenceDescription;
            NodeId root = (NodeId)rd.NodeId;

            CreateTreeView(root, e.Node);
        }

        private void CreateTreeView(NodeId rootNodeId, TreeNode treeNode)
        {
            ReferenceDescriptionCollection rdcOrg = _browser.Browse(rootNodeId);

            foreach (ReferenceDescription referenceDes in rdcOrg)
            {
                TreeNode tn = treeNode.Nodes.Add(referenceDes.DisplayName.Text);
                tn.Tag = referenceDes;
                tn.Nodes.Add(string.Empty);
            }
        }
    }
}
