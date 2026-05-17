(function () {
    function buildInfiniteTicker() {
        const track = document.getElementById('tickerTrack');
        if (!track) return;

        const originalItems = Array.from(track.children);

        if (!originalItems.length) return;

        const originalHtml = originalItems
            .map(item => item.outerHTML)
            .join('');

        // Reset first, so browser refresh/back navigation doesn't keep duplicating forever.
        track.innerHTML = originalHtml;

        let tickerHalfHtml = originalHtml;
        track.innerHTML = tickerHalfHtml;

        /*
           Make the first half wider than the viewport.
           Then duplicate that completed half.
           The CSS scrolls exactly -50%, so it loops seamlessly.
        */
        const minimumWidth = window.innerWidth * 1.5;

        while (track.scrollWidth < minimumWidth) {
            tickerHalfHtml += originalHtml;
            track.innerHTML = tickerHalfHtml;
        }

        track.innerHTML = tickerHalfHtml + tickerHalfHtml;
    }

    document.addEventListener('DOMContentLoaded', buildInfiniteTicker);
})();