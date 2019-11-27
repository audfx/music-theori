using System;
using System.Collections.Generic;

using theori.Platform;
using theori.Resources;

namespace theori
{
    public static class Host
    {
        private static ClientResourceManager? staticResources = null;

        public static ClientResourceManager StaticResources => staticResources ?? throw new InvalidOperationException();

        public static ClientHost GetSuitableHost(ClientResourceLocator? staticResourceLocator = null)
        {
            staticResources = new ClientResourceManager(staticResourceLocator ?? ClientResourceLocator.Default);

            // TODO(local): switch on things, get better hosts or error when can't get one.
            return new DesktopClientHost();
        }

#if false
        private static bool ProcessLayerStackChanges()
        {
            bool processed = layerStackChanges.Count > 0;

            foreach (var change in layerStackChanges)
            {
                bool isOverlay = change.IsOverlayChange;

                switch (change.Operation)
                {
                    case LayerStackOp.AddAfter:
                    {
                        var aboveThis = change.RelativeTo!;
                        var layer = change.Layer;

                        if (isOverlay)
                        {
                            if (aboveThis == null)
                                overlays.Add((Overlay)layer);
                            else overlays.Insert(overlays.IndexOf((Overlay)aboveThis) + 1, (Overlay)layer);
                        }
                        else
                        {
                            if (aboveThis == null)
                                layers.Add(layer);
                            else
                            {
                                if (!layers.Contains(aboveThis))
                                    throw new Exception("Cannot add a layer above one which is not in the layer stack.");

                                int index = layers.IndexOf(aboveThis);
                                layers.Insert(index + 1, layer);

                                if (layer.BlocksParentLayer)
                                {
                                    for (int i = index; i >= 0; i--)
                                    {
                                        var nextLayer = layers[i];
                                        nextLayer.Suspend(null);

                                        if (nextLayer.BlocksParentLayer)
                                        {
                                            // if it blocks the previous layers then this has happened already for higher layers.
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        layer.Init();
                        layer.lifetimeState = Layer.LayerLifetimeState.Alive;
                    } break;

                    case LayerStackOp.AddBefore:
                    {
                        var belowThis = change.RelativeTo!;
                        var layer = change.Layer;

                        if (isOverlay)
                            overlays.Insert(overlays.IndexOf((Overlay)belowThis), (Overlay)layer);
                        else
                        {
                            if (!layers.Contains(belowThis))
                                throw new Exception("Cannot add a layer above one which is not in the layer stack.");

                            int index = layers.IndexOf(belowThis);
                            layers.Insert(index, layer);

                            if (layer.BlocksParentLayer)
                            {
                                for (int i = index - 1; i >= 0; i--)
                                {
                                    var nextLayer = layers[i];
                                    nextLayer.Suspend(null);

                                    if (nextLayer.BlocksParentLayer)
                                    {
                                        // if it blocks the previous layers then this has happened already for higher layers.
                                        break;
                                    }
                                }
                            }

                            if (belowThis.BlocksParentLayer)
                            {
                                for (int i = index; i >= 0; i--)
                                {
                                    var nextLayer = layers[i];
                                    nextLayer.Suspend(null);

                                    if (nextLayer.BlocksParentLayer)
                                    {
                                        // if it blocks the previous layers then this has happened already for higher layers.
                                        break;
                                    }
                                }
                            }
                        }

                        layer.Init();
                        layer.lifetimeState = Layer.LayerLifetimeState.Alive;
                    } break;

                    case LayerStackOp.Remove:
                    {
                        var layer = change.Layer;

                        if (layer.lifetimeState != Layer.LayerLifetimeState.Alive)
                            throw new ArgumentException("Layer to remove was not alive.");

                        layer.DestroyInternal();
                        layer.lifetimeState = Layer.LayerLifetimeState.Destroyed;

                        if (isOverlay)
                            overlays.Remove((Overlay)layer);
                        else
                        {
                            int index = layers.IndexOf(layer);
                            layers.RemoveAt(index);

                            if (!layer.IsSuspended)
                            {
                                if (layer.BlocksParentLayer)
                                {
                                    int startIndex = index - 1;
                                    for (; startIndex >= 0; startIndex--)
                                    {
                                        if (layers[startIndex].BlocksParentLayer)
                                        {
                                            // if it blocks the previous layers then this will happen later for higher layers.
                                            break;
                                        }
                                    }

                                    // resume layers bottom to top
                                    for (int i = MathL.Max(0, startIndex); i < LayerCount; i++)
                                        layers[i].Resume(null);
                                }
                            }
                        }
                    } break;
                }
            }

            layerStackChanges.Clear();
            if (LayerCount == 0)
                runProgramLoop = false;

            return processed;
        }
#endif
    }
}
