﻿using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Skclusive.Material.Core;
using Skclusive.Material.Menu;

namespace SCMM.Web.Client.Shared.Component.ContextMenu
{
    public class ContextContainerComponent : MaterialComponent
    {
        public ContextContainerComponent() : base("ContextContainer")
        {
        }

        [Parameter]
        public RenderFragment ContextMenu { get; set; }

        [Parameter]
        public bool Open { set; get; }

        protected double MouseX { get; set; }

        protected double MouseY { get; set; }

        protected void HandleMenuOpen(MouseEventArgs args)
        {
            // TODO: Make this accessible on mobile devices by implementing a "long click" option
            if (args.Button == 2)
            {
                MouseX = args.ClientX;
                MouseY = args.ClientY;
                Open = true;
                StateHasChanged();
            }
        }

        protected void HandleMenuClose(EventArgs args)
        {
            CloseMenu();
        }

        protected void HandleMenuClose(MenuCloseReason reason)
        {
            CloseMenu();
        }

        public void CloseMenu()
        {
            Open = false;
            StateHasChanged();
        }
    }
}
