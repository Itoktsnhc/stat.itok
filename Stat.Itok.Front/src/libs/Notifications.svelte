<script type="ts">
    let nMsg = "";
    let hide = true;
    let msgType = "is-warning";
    export function showNotice(msg: string, msgType: string) {
        msgType = msgType;
        nMsg = msg;
        hide = false;
        setTimeout(() => {
            hide = true;
        }, 5000);
    }
</script>

<div hidden={hide || null} class="notification {msgType} is-light">
    <button class="delete" />
    {nMsg}
</div>
