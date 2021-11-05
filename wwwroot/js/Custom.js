let index = 0;

function AddTag() {
    //get a reference to the tag entry input element
    var tagEntry = document.getElementById("TagEntry");

    //use search function to help detect an error state
    let searchResult = search(tagEntry.value);
    if (searchResult != null) {
        //trigger sweet alert for whatever condition is contained in the search result variable
        Swal.fire({
            icon: 'error',
            title: 'Oops!',
            text: searchResult,
            confirmButtonColor: '#212422',
            width: 400,
            timer: 4000,
            timerProgressBar: true,

        })
        tagEntry.value = "";
    }
    else {
        //create a new select option
        let newOption = new Option(tagEntry.value, tagEntry.value);
        document.getElementById("TagList").options[index++] = newOption;

        //clear out the tag entry control
        tagEntry.value = "";
        return true;

    }




}

function DeleteTag() {
    let tagCount = 1;
    while (tagCount > 0) {
        let tagList = document.getElementById("TagList");
        let selectedIndex = tagList.selectedIndex;
        if (selectedIndex >= 0) {
            tagList.options[selectedIndex] = null;
            --tagCount;
        }
        else
            tagCount = 0;
        index--;
    }

}

$("form").on("submit", function () {
    $("#TagList option").prop("selected", "selected");
})

//look for the tag values variable to see if it has data. 
if (tagValues != "") {
    let tagArray = tagValues.split(",");
    for (let loop = 0; loop < tagArray.length; loop++) {
        //load up or replace the options that we have 
        ReplaceTag(tagArray[loop], loop);
        index++;
    }
}

function ReplaceTag(tag, index) {
    let newOption = new Option(tag, tag);
    document.getElementById("TagList").options[index] = newOption;
}

//the search function will detect either an empty or a duplicate Tag
//and return an error string if an error is detected

function search(str) {
    if (str == "") {
        return "Empty tags are not permitted.";
    }
    var tagsEl = document.getElementById("TagList");
    if (tagsEl) {
        let options = tagsEl.options;
        for (let i = 0; i < options.length; i++) {
            if (options[i].value == str) {
                return `The tag #${str} was detected as a duplicate and isn't permitted.`;
            }
        }
    }
}