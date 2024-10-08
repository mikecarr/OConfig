using Avalonia.Controls;
using System.Collections.Generic;

public class YamlTreeBuilder
{
    private TreeView _treeView;

    public YamlTreeBuilder(TreeView treeView)
    {
        _treeView = treeView;
    }

    public void BuildTree(Dictionary<string, object> yamlData)
    {
        // Clear existing items before adding new ones
        _treeView.Items.Clear();
        
        foreach (var node in CreateTreeNodes(yamlData))
        {
            _treeView.Items.Add(node);
        }
    }

    private IEnumerable<TreeViewItem> CreateTreeNodes(Dictionary<string, object> yamlData)
    {
        foreach (var kvp in yamlData)
        {
            var treeViewItem = new TreeViewItem
            {
                Header = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = kvp.Key + ": " }, // Label for key
                        CreateValueEditor(kvp.Value) // TextBox or recursive child nodes for value
                    }
                }
            };

            if (kvp.Value is Dictionary<string, object> childDictionary)
            {
                // Recursively create nodes for child dictionaries
                foreach (var childNode in CreateTreeNodes(childDictionary))
                {
                    treeViewItem.Items.Add(childNode); // Add child nodes to the current tree view item
                }
            }
            else if (kvp.Value is IList<object> list)
            {
                // Handle lists (arrays in YAML)
                foreach (var item in list)
                {
                    var listItem = new TreeViewItem
                    {
                        Header = new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock { Text = "[List Item]: " },
                                CreateValueEditor(item)
                            }
                        }
                    };

                    treeViewItem.Items.Add(listItem); // Add list item to the current tree view item
                }
            }

            yield return treeViewItem;
        }
    }

    private Control CreateValueEditor(object value)
    {
        // If the value is a dictionary, create a placeholder for complex values
        if (value is Dictionary<string, object>)
        {
            return new TextBlock { Text = "[Complex Value]" }; // Placeholder for complex values
        }
        // Create a TextBox for editing primitive values
        else
        {
            return new TextBox
            {
                Text = value?.ToString() ?? string.Empty,
                Width = 200,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };
        }
    }
}
