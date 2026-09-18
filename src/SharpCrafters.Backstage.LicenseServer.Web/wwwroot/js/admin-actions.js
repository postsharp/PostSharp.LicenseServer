// The management actions of a license. This file closes the menu when the administrator clicks
// outside it, and it asks for a confirmation before an action runs.
//
// Both behaviours are additions to markup that already works. The menu is a <details> element, which
// opens and closes without a script, and a form whose submission this file does not intercept is
// submitted by the browser. A browser that does not run this file therefore keeps every action, and
// loses only the confirmation.

(function () {
    "use strict";

    var menus = Array.prototype.slice.call( document.querySelectorAll( "[data-menu]" ) );

    function closeMenus( except ) {
        menus.forEach( function ( menu ) {
            if ( menu !== except ) {
                menu.open = false;
            }
        } );
    }

    document.addEventListener( "click", function ( event ) {
        var target = event.target;

        closeMenus( target && target.closest ? target.closest( "[data-menu]" ) : null );
    } );

    document.addEventListener( "keydown", function ( event ) {
        if ( event.key === "Escape" ) {
            closeMenus( null );
        }
    } );

    var dialog = document.getElementById( "confirm-dialog" );

    // In a browser that does not support <dialog>, the form is submitted directly, so the action is
    // not lost.
    if ( !dialog || typeof dialog.showModal !== "function" ) {
        return;
    }

    var title = document.getElementById( "confirm-title" );
    var detail = document.getElementById( "confirm-detail" );
    var okButton = dialog.querySelector( "[data-confirm-ok]" );
    var pendingForm = null;

    document.querySelectorAll( "form[data-confirm]" ).forEach( function ( form ) {
        form.addEventListener( "submit", function ( event ) {
            event.preventDefault();

            pendingForm = form;
            title.textContent = form.dataset.confirm;
            detail.textContent = form.dataset.confirmDetail || "";
            okButton.textContent = form.dataset.confirmAction || "Continue";
            okButton.classList.toggle( "is-destructive", form.dataset.confirmDestructive === "true" );

            closeMenus( null );

            // The value is cleared explicitly. In some browsers, a dialog closed with the Escape key
            // keeps the value of the previous close, which would confirm an action that the
            // administrator did not confirm.
            dialog.returnValue = "";
            dialog.showModal();
        } );
    } );

    dialog.addEventListener( "close", function () {
        var form = pendingForm;
        pendingForm = null;

        if ( form && dialog.returnValue === "confirm" ) {
            // HTMLFormElement.submit does not raise the submit event, so the confirmation is not
            // asked a second time.
            form.submit();
        }
    } );
})();
