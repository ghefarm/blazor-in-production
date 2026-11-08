// The PayPal subscribe button on /subscribe. PayPal's script is loaded once,
// with the client id the API reports, so the button and the server always
// talk to the same PayPal: the sandbox or the live one.
let foresightPayPal = null;

function loadPayPal(clientId) {
    foresightPayPal ??= new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://www.paypal.com/sdk/js?client-id='
            + encodeURIComponent(clientId)
            + '&vault=true&intent=subscription';
        script.onload = resolve;
        script.onerror = () => {
            foresightPayPal = null; // let the next visit try again
            reject(new Error('PayPal could not be loaded.'));
        };
        document.head.appendChild(script);
    });
    return foresightPayPal;
}

// account is the signed-in account's email. PayPal keeps it with the
// subscription as custom_id and returns it whenever it is asked.
window.showSubscribeButton = async (container, clientId, planId,
    account, page) => {
    await loadPayPal(clientId);
    paypal.Buttons({
        createSubscription: (data, actions) => actions.subscription.create({
            plan_id: planId,
            custom_id: account,
            // Premium is digital, so PayPal asks for no shipping address.
            application_context: { shipping_preference: 'NO_SHIPPING' }
        }),
        // Only the id goes to our server. The server asks PayPal the rest.
        onApprove: data =>
            page.invokeMethodAsync('OnApprovedAsync', data.subscriptionID)
    }).render(container);
};
